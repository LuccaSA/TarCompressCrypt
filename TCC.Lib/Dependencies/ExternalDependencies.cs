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

        public string GetPath(ExternalDep dependency)
        {
            string exePath = Path.Combine(Root, dependency.ExtractFolder, dependency.ExeName);
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
            return exePath.Escape();
        }

        public async Task<string> EnsureDependency(ExternalDep dependency, IProgress<int> percent)
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

        private async Task DownloadDependencyInternal(ExternalDep dependency, IProgress<int> percent, string path, string exePath)
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

        private static readonly ExternalDep _lz4 = new ExternalDep
        {
            Name = "Lz4",
            Url = @"https://github.com/lz4/lz4/releases/download/v1.8.3/lz4_v1_8_3_win64.zip",
            ZipFilename = "lz4_v1_8_3_win64.zip",
            ExtractFolder = "lz4_v183",
            ExeName = "lz4.exe"
        };

        private static readonly ExternalDep _brotli = new ExternalDep
        {
            Name = "Brotli",
            Url = @"https://github.com/google/brotli/releases/download/v1.0.4/brotli-v1.0.4-win_x86_64.zip",
            ZipFilename = "brotli-v1.0.4-win_x86_64.zip",
            ExtractFolder = "brotli_v104",
            ExeName = "brotli.exe"
        };

        private static readonly ExternalDep _zStd = new ExternalDep
        {
            Name = "Zstandard",
            Url = @"https://github.com/facebook/zstd/releases/download/v1.3.5/zstd-v1.3.5-win64.zip",
            ZipFilename = "zstd-v1.3.5-win64.zip",
            ExtractFolder = "zstd_v135",
            ExeName = "zstd.exe"
        };

        // From : https://bintray.com/vszakats/generic/openssl
        // referenced from official OpenSSL wiki : https://wiki.openssl.org/index.php/Binaries
        private static readonly ExternalDep _openSsl = new ExternalDep
        {
            Name = "OpenSSL",
            Url = @"https://bintray.com/vszakats/generic/download_file?file_path=openssl-1.1.1-win64-mingw.zip",
            ZipFilename = "openssl-1.1.1-win64-mingw.zip",
            ExtractFolder = "openssl_v111",
            ExeName = "openssl-1.1.1-win64-mingw\\openssl.exe"
        };

        private static readonly ExternalDep _tar = new ExternalDep
        {
            Name = "Tar",
            Url = @"https://github.com/rducom/TarCompressCrypt/raw/master/Dependencies/tar_msys2_130.zip",
            ZipFilename = "tar_msys2_130.zip",
            ExtractFolder = "tar_v130",
            ExeName = "tar.exe"
        };

    }
}