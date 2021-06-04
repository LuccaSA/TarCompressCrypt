using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests
{
    public class BlobStorageTests : IClassFixture<EnvVarFixture>
    {
        private EnvVarFixture _envVarFixture;

        public BlobStorageTests(EnvVarFixture envVarFixture)
        {
            _envVarFixture = envVarFixture;
        }

        [Fact(Skip = "desactivated accound")]
        public async Task AzcopyUploadTest()
        {
            var dep = new ExternalDependencies(new NullLogger<ExternalDependencies>());
            await dep.EnsureDependency(ExternalDependencies._azCopy);
            dep.GetPath(ExternalDependencies._azCopy).EnsureFileExists();

            var up = new UploadCommands(new ExternalDependencies(new NullLogger<ExternalDependencies>()));

            string toCompressFolder = TestFileHelper.NewFolder();
            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            var opt = new CompressOption()
            {
                AzBlobUrl = GetEnvVar("AZ_URL"),
                AzBlobContainer = GetEnvVar("AZ_CONTAINER"),
                AzSaS = GetEnvVar("AZ_SAS_TOKEN")
            };

            var cmd = up.UploadCommand(opt, data.Files.First(), new DirectoryInfo(toCompressFolder));

            bool success = await TarCompressCrypt.AzCopyUploadOnBlobAsync(toCompressFolder, cmd, CancellationToken.None);

            Assert.True(success);
        }

        [Fact(Skip = "desactivated accound")]
        public async Task SdkUploadTest()
        {
            var dep = new ExternalDependencies(new NullLogger<ExternalDependencies>());
            await dep.EnsureDependency(ExternalDependencies._azCopy);
            dep.GetPath(ExternalDependencies._azCopy).EnsureFileExists();
             
            string toCompressFolder = TestFileHelper.NewFolder();
            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            var opt = new CompressOption()
            {
                AzBlobUrl = GetEnvVar("AZ_URL"),
                AzBlobContainer = GetEnvVar("AZ_CONTAINER"),
                AzSaS = GetEnvVar("AZ_SAS_TOKEN")
            };

           var ok = await TarCompressCrypt.SdkUploadOnBlobAsync(opt,
                new DirectoryInfo(toCompressFolder),
                data.Files.First(),
                CancellationToken.None);
           
           Assert.True(ok.success);
        }

        string GetEnvVar(string key)
        {
            var s = Environment.GetEnvironmentVariable(key);
            Assert.True(s != null, key);
            Assert.True(!string.IsNullOrWhiteSpace(s),key);
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
