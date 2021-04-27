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
