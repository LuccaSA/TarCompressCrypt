using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests
{
    public class DependencyTests
    {
        [Fact]
        public async Task TestDependenciesAsync()
        {
            var dep = new ExternalDependencies(new NullLogger<ExternalDependencies>());

            await dep.EnsureDependency(ExternalDependencies._lz4);
            dep.GetPath(ExternalDependencies._lz4).EnsureFileExists();

            await dep.EnsureDependency(ExternalDependencies._brotli);
            dep.GetPath(ExternalDependencies._brotli).EnsureFileExists();

            await dep.EnsureDependency(ExternalDependencies._zStd);
            dep.GetPath(ExternalDependencies._zStd).EnsureFileExists();

            await dep.EnsureDependency(ExternalDependencies._openSsl);
            dep.GetPath(ExternalDependencies._openSsl).EnsureFileExists();

            await dep.EnsureDependency(ExternalDependencies._tar);
            dep.GetPath(ExternalDependencies._tar).EnsureFileExists();

            await dep.EnsureDependency(ExternalDependencies._azCopy);
            dep.GetPath(ExternalDependencies._azCopy).EnsureFileExists();
        }

        [Fact]
        public async Task AzureUploadTest()
        {
            var dep = new ExternalDependencies(new NullLogger<ExternalDependencies>());
            await dep.EnsureDependency(ExternalDependencies._azCopy);
            dep.GetPath(ExternalDependencies._azCopy).EnsureFileExists();

            string GetEnvVar(string key)
            {
                var s = Environment.GetEnvironmentVariable("AZ_URL");
                Assert.NotNull(s);
                Assert.NotEmpty(s);
                return s;
            }

            var up = new UploadCommands(new ExternalDependencies(new NullLogger<ExternalDependencies>()));

            string toCompressFolder = TestFileHelper.NewFolder();
            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            var opt = new CompressOption()
            {
                AzBlob = GetEnvVar("AZ_URL"),
                AzSaS = GetEnvVar("AZ_SAS_TOKEN")
            };
           
            var cmd = up.UploadCommand(opt, data.Files.First(), new DirectoryInfo(toCompressFolder));

            bool success = await TarCompressCrypt.UploadOnBlobAsync(toCompressFolder, cmd, CancellationToken.None);

            Assert.True(success);
        }
    }

    public static class DependencyHelper
    {

        public static void EnsureFileExists(this string path)
        {
            bool exists = File.Exists(path);
            Assert.True(exists, path);
        }
    }
}
