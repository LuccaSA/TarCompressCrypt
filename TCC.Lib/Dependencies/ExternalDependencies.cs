using System;
using System.IO;
using System.IO.Compression;
using System.Net;
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

        public string Tar() => GetPath(_tar);
        public string Lz4() => GetPath(_lz4);
        public string Brotli() => GetPath(_brotli);
        public string Zstd() => GetPath(_zStd);
        public string OpenSsl() => GetPath(_openSsl);

        public async Task EnsureAllDependenciesPresent()
        {
            var progress = new Progress<int>();
            await EnsureDependency(_lz4, progress);
            await EnsureDependency(_brotli, progress);
            await EnsureDependency(_zStd, progress);
            await EnsureDependency(_openSsl, progress);
            await EnsureDependency(_tar, progress);
        }

        public string Root => Path.GetDirectoryName(typeof(ExternalDependencies).Assembly.CodeBase.Replace("file:///", ""));

        public string GetPath(Dependency dependency)
        {
            string exePath = Path.Combine(Root, dependency.ExtractFolder, dependency.ExeName);
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
            return exePath.Escape();
        }

        public async Task<string> EnsureDependency(Dependency dependency, IProgress<int> percent)
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
                        await DownloadDependencyInternal(dependency, percent, path, exePath);
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "Critical error while downloading external dependency");
                    }
                });
            }

            percent.Report(100);
            return exePath;
        }

        private async Task DownloadDependencyInternal(Dependency dependency, IProgress<int> percent, string path, string exePath)
        {
            if (File.Exists(exePath))
            {
                percent.Report(100);
                return;
            }

            string target = Path.Combine(path, string.IsNullOrEmpty(dependency.ZipFilename)
                ? dependency.ExeName
                : dependency.ZipFilename);

            Directory.CreateDirectory(path);

            _logger.LogInformation($"Downloading {dependency.Name} from {dependency.Url} ");

            using (var wc = new WebClient())
            {
                wc.DownloadProgressChanged += (s, e) =>
                {
                    percent.Report((int)(e.BytesReceived / e.TotalBytesToReceive * 100));
                };
                wc.DownloadFileCompleted += (s, e) =>
                {
                    percent.Report(100);
                };
                await wc.DownloadFileTaskAsync(dependency.Url, target);
            }

            _logger.LogInformation($"Download finished for {dependency.Name}");

            if (!string.IsNullOrEmpty(dependency.ZipFilename))
            {
                ZipFile.ExtractToDirectory(target, path);
            }

            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
        }

        private static readonly Dependency _lz4 = new Dependency
        {
            Name = "Lz4",
            Url = @"https://github.com/lz4/lz4/releases/download/v1.9.2/lz4_win64_v1_9_2.zip",
            ZipFilename = "lz4_v1_9_2_win64.zip",
            ExtractFolder = "lz4_v183",
            ExeName = "lz4.exe"
        };

        private static readonly Dependency _brotli = new Dependency
        {
            Name = "Brotli",
            Url = @"https://github.com/google/brotli/releases/download/v1.0.4/brotli-v1.0.4-win_x86_64.zip",
            ZipFilename = "brotli-v1.0.4-win_x86_64.zip",
            ExtractFolder = "brotli_v104",
            ExeName = "brotli.exe"
        };

        private static readonly Dependency _zStd = new Dependency
        {
            Name = "Zstandard",
            Url = @"https://github.com/facebook/zstd/releases/download/v1.4.8/zstd-v1.4.8-win64.zip",
            ZipFilename = "zstd-v1.4.8-win64.zip",
            ExtractFolder = "zstd_v148",
            ExeName = "zstd.exe"
        };

        // From : https://bintray.com/vszakats/generic/openssl
        // referenced from official OpenSSL wiki : https://wiki.openssl.org/index.php/Binaries
        private static readonly Dependency _openSsl = new Dependency
        {
            Name = "OpenSSL",
            Url = @"https://bintray.com/vszakats/generic/download_file?file_path=openssl-1.1.1d-win64-mingw.zip",
            ZipFilename = "openssl-1.1.1d-win64-mingw.zip",
            ExtractFolder = "openssl_v111d",
            ExeName = "openssl-1.1.1d-win64-mingw\\openssl.exe"
        };

        private static readonly Dependency _tar = new Dependency
        {
            Name = "Tar",
            Url = @"https://github.com/LuccaSA/TarCompressCrypt/raw/master/Dependencies/tar_msys2_130.zip",
            ZipFilename = "tar_msys2_130.zip",
            ExtractFolder = "tar_v130",
            ExeName = "tar.exe"
        };

    }
}