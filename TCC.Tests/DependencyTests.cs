using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TCC.Lib.Dependencies;
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
    public static class DependencyHelper { 

        public static void EnsureFileExists(this string path)
        {
            bool exists = File.Exists(path);
            Assert.True(exists,path);
        }
    }
}
