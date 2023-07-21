﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tes.Models;
using TesApi.Web;
using TesApi.Web.Options;
using TesApi.Web.Storage;

namespace TesApi.Tests.Storage
{
    [TestClass, TestCategory("Unit")]
    public class DefaultStorageAccessProviderTests
    {
        private DefaultStorageAccessProvider defaultStorageAccessProvider;
        private Mock<IAzureProxy> azureProxyMock;
        private StorageOptions storageOptions;
        private StorageAccountInfo storageAccountInfo;
        private const string DefaultStorageAccountName = "defaultstorage";
        private const string StorageAccountBlobEndpoint = $"https://{DefaultStorageAccountName}.blob.windows.net";

        [TestInitialize]
        public void Setup()
        {
            azureProxyMock = new Mock<IAzureProxy>();
            storageOptions = new StorageOptions() { DefaultAccountName = DefaultStorageAccountName };
            var subscriptionId = Guid.NewGuid().ToString();
            storageAccountInfo = new StorageAccountInfo()
            {
                BlobEndpoint = StorageAccountBlobEndpoint,
                Name = DefaultStorageAccountName,
                Id = $"/subscriptions/{subscriptionId}/resourceGroups/mrg/providers/Microsoft.Storage/storageAccounts/{DefaultStorageAccountName}",
                SubscriptionId = subscriptionId
            };
            azureProxyMock.Setup(p => p.GetStorageAccountKeyAsync(It.IsAny<StorageAccountInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(GenerateRandomTestAzureStorageKey());
            azureProxyMock.Setup(p => p.GetStorageAccountInfoAsync(It.Is<string>(s => s.Equals(DefaultStorageAccountName)), It.IsAny<CancellationToken>())).ReturnsAsync(storageAccountInfo);
            defaultStorageAccessProvider = new DefaultStorageAccessProvider(NullLogger<DefaultStorageAccessProvider>.Instance, Options.Create(storageOptions), azureProxyMock.Object);
        }

        [TestMethod]
        [DataRow("script/foo.sh")]
        [DataRow("/script/foo.sh")]
        public async Task GetInternalTesTaskBlobUrlAsync_BlobPathIsProvided_ReturnsValidURLWithDefaultStorageAccountTesInternalContainerAndTaskId(
            string blobName)
        {
            var task = new TesTask { Name = "taskName", Id = Guid.NewGuid().ToString() };
            var url = await defaultStorageAccessProvider.GetInternalTesTaskBlobUrlAsync(task, blobName, CancellationToken.None);

            Assert.IsNotNull(url);
            var uri = new Uri(url);
            Assert.AreEqual($"{StorageAccountBlobEndpoint}{StorageAccessProvider.TesExecutionsPathPrefix}/{task.Id}/{blobName.TrimStart('/')}", ToHostWithAbsolutePathOnly(uri));
        }

        private static string ToHostWithAbsolutePathOnly(Uri uri)
        {
            return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
        }

        [TestMethod]
        [DataRow("script/foo.sh")]
        [DataRow("/script/foo.sh")]
        public async Task GetInternalTesTaskBlobUrlAsync_BlobPathAndInternalPathPrefixIsProvided_ReturnsValidURLWithDefaultStorageAccountAndInternalPathPrefixAppended(
            string blobName)
        {
            var internalPathPrefix = "internalPathPrefix";

            var task = new TesTask { Name = "taskName", Id = Guid.NewGuid().ToString() };
            task.Resources = new TesResources();
            task.Resources.BackendParameters = new Dictionary<string, string>
            {
                { TesResources.SupportedBackendParameters.internal_path_prefix.ToString(), internalPathPrefix }
            };
            var url = await defaultStorageAccessProvider.GetInternalTesTaskBlobUrlAsync(task, blobName, CancellationToken.None);

            Assert.IsNotNull(url);
            var uri = new Uri(url);
            Assert.AreEqual($"{StorageAccountBlobEndpoint}/{internalPathPrefix}/{blobName.TrimStart('/')}", ToHostWithAbsolutePathOnly(uri));
        }

        private static string GenerateRandomTestAzureStorageKey()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
            var length = 64;
            var random = new Random();
            var result = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }
    }
}