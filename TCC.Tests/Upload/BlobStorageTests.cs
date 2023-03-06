using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Options;
using TCC.Lib.Storage;
using Xunit;

namespace TCC.Tests.Upload
{
    public class UploadToStorageTests : IClassFixture<EnvVarFixture>
    {
        private EnvVarFixture _envVarFixture;

        public UploadToStorageTests(EnvVarFixture envVarFixture)
        {
            _envVarFixture = envVarFixture;
        }

        [Fact(Skip = "desactivated accound")]
        public async Task AzureUploadTest()
        {
            string toCompressFolder = TestFileHelper.NewFolder();
            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            var opt = new CompressOption()
            {
                AzBlobUrl = GetEnvVar("AZ_URL"),
                AzBlobContainer = GetEnvVar("AZ_CONTAINER"),
                AzBlobSaS = GetEnvVar("AZ_SAS_TOKEN")
            };
            opt.UploadModes = new List<UploadMode>() { UploadMode.AzureSdk };
            var uploader = await opt.GetRemoteStoragesAsync(NullLogger.Instance, CancellationToken.None).ToListAsync();

            var ok = await uploader.First().UploadAsync(data.Files.First(), new DirectoryInfo(toCompressFolder), CancellationToken.None);

            Assert.True(ok.IsSuccess);
        }

        [Fact]
        public async Task GoogleUploadTest()
        {
            string toCompressFolder = TestFileHelper.NewFolder();
            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            var opt = new CompressOption()
            {
                GoogleStorageBucketName = GetEnvVar("GoogleBucket"),
                GoogleStorageCredential = GetEnvVar("GoogleCredential")
            };

            opt.UploadModes = new List<UploadMode>() { UploadMode.GoogleCloudStorage };
            var uploader = await opt.GetRemoteStoragesAsync(NullLogger.Instance, CancellationToken.None).ToListAsync();

            var ok = await uploader.First().UploadAsync(data.Files.First(), new DirectoryInfo(toCompressFolder), CancellationToken.None);

            Assert.True(ok.IsSuccess);

            var gs = uploader.First() as GoogleRemoteStorage;
            
            await gs.Storage.DeleteObjectAsync(gs.BucketName, ok.RemoteFilePath);
        }

        string GetEnvVar(string key)
        {
            var s = Environment.GetEnvironmentVariable(key);
            Assert.True(s != null, key);
            Assert.True(!string.IsNullOrWhiteSpace(s), key);
            return s;
        }

    }


    public class EnvVarFixture : IDisposable
    {
        public EnvVarFixture()
        {
            if (File.Exists("Properties\\launchSettings.json"))
            {
                using var file = File.OpenText("Properties\\launchSettings.json");
                var reader = new JsonTextReader(file);
                var jObject = JObject.Load(reader);

                var variables = jObject
                    .GetValue("profiles")
                    //select a proper profile here
                    .SelectMany(profiles => profiles.Children())
                    .SelectMany(profile => profile.Children<JProperty>())
                    .Where(prop => prop.Name == "environmentVariables")
                    .SelectMany(prop => prop.Value.Children<JProperty>())
                    .ToList();

                foreach (var variable in variables)
                {
                    Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
