using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TCC.Lib.Helpers;

namespace TCC.Lib.Dependencies
{
    public class ExternalDependencies
    {
        public ExternalDependencies(ILogger<ExternalDependencies> logger)
        {
            _logger = logger;
        }

        private readonly ILogger<ExternalDependencies> _logger;

        public string Tar() => GetPathEscaped(_tar);
        public string Lz4() => GetPathEscaped(_lz4);
        public string Brotli() => GetPathEscaped(_brotli);
        public string Zstd() => GetPathEscaped(_zStd);
        public string OpenSsl() => GetPathEscaped(_openSsl);

        public async Task EnsureAllDependenciesPresent()
        {
            await EnsureDependency(_lz4);
            await EnsureDependency(_brotli);
            await EnsureDependency(_zStd);
            await EnsureDependency(_openSsl);
            await EnsureDependency(_tar);
        }

        private string Root => Path.GetDirectoryName(AppContext.BaseDirectory);

        private string GetPathEscaped(Dependency dependency)
        {
            return GetPath(dependency).Escape();
        }
        
        internal string GetPath(Dependency dependency)
        {
            string exePath = Path.Combine(Root, dependency.ExtractFolder, dependency.ExeName);
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
            return exePath;
        }

        public async Task<string> EnsureDependency(Dependency dependency)
        {
            string path = Path.Combine(Root, dependency.ExtractFolder);
            string exePath = Path.Combine(Root, dependency.ExtractFolder, dependency.ExeName);
            var downloadLock = new FileInfo(Path.Combine(Root, dependency.Name + ".lock"));

            if (!Directory.Exists(path) || !File.Exists(exePath))
            {
                await downloadLock.Lock(async () =>
                {
                    try
                    {
                        await DownloadDependencyInternal(dependency, path, exePath);
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "Critical error while downloading external dependency");
                    }
                });
            }
             
            return exePath;
        }

        private async Task DownloadDependencyInternal(Dependency dependency, string path, string exePath)
        {
            if (File.Exists(exePath))
            {
                return;
            }

            string target = Path.Combine(path, string.IsNullOrEmpty(dependency.ZipFilename)
                ? dependency.ExeName
                : dependency.ZipFilename);

            Directory.CreateDirectory(path);

            _logger.LogInformation($"Downloading {dependency.Name} from {dependency.Url} ");

            using (var _httpClient = new HttpClient())
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(dependency.Url);
                File.WriteAllBytes(target, fileBytes);
            }

            _logger.LogInformation($"Download finished for {dependency.Name}");

            if (!string.IsNullOrEmpty(dependency.ZipFilename))
            {
                ZipFile.ExtractToDirectory(target, path,true);
            }

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
        }

        internal static readonly Dependency _lz4 = new Dependency
        {
            Name = "Lz4",
            Url = @"https://github.com/lz4/lz4/releases/download/v1.9.2/lz4_win64_v1_9_2.zip",
            ZipFilename = "lz4_v1_9_2_win64.zip",
            ExtractFolder = "lz4_v183",
            ExeName = "lz4.exe"
        };

        internal static readonly Dependency _brotli = new Dependency
        {
            Name = "Brotli",
            Url = @"https://github.com/google/brotli/releases/download/v1.0.4/brotli-v1.0.4-win_x86_64.zip",
            ZipFilename = "brotli-v1.0.4-win_x86_64.zip",
            ExtractFolder = "brotli_v104",
            ExeName = "brotli.exe"
        };

        internal static readonly Dependency _zStd = new Dependency
        {
            Name = "Zstandard",
            Url = @"https://github.com/facebook/zstd/releases/download/v1.4.8/zstd-v1.4.8-win64.zip",
            ZipFilename = "zstd-v1.4.8-win64.zip",
            ExtractFolder = "zstd_v148",
            ExeName = "zstd.exe"
        };

        // From : https://curl.se/windows/
        // referenced from official OpenSSL wiki : https://wiki.openssl.org/index.php/Binaries
        internal static readonly Dependency _openSsl = new Dependency
        {
            Name = "OpenSSL",
            Url = @"https://curl.se/windows/dl-7.81.0/openssl-3.0.1-win64-mingw.zip",
            ZipFilename = "openssl-3.0.1-win64-mingw.zip",
            ExtractFolder = "openssl_v301",
            ExeName = "openssl-3.0.1-win64-mingw\\bin\\openssl.exe"
        };

        internal static readonly Dependency _tar = new Dependency
        {
            Name = "Tar",
            Url = @"https://github.com/LuccaSA/TarCompressCrypt/raw/master/Dependencies/tar_msys2_130.zip",
            ZipFilename = "tar_msys2_130.zip",
            ExtractFolder = "tar_v130",
            ExeName = "tar.exe"
        };
    }
}