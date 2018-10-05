using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using TCC.Lib.Helpers;

namespace TCC.Lib.Dependencies
{
    public class ExternalDependencies
    {
        private const string ExeTar = @"Libs\tar.exe";

        public string Tar()
        {
            string tarPath = Path.Combine(Root, ExeTar);
            if (!File.Exists(tarPath))
            {
                throw new FileNotFoundException("tar not found in " + tarPath);
            }
            return tarPath.Escape();
        }

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
            string downloadLock = Path.Combine(Root, dependency.Name + ".lock");
            if (!Directory.Exists(path) || !File.Exists(exePath))
            {
                using (new FileStream(downloadLock, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None))
                {
                    if (File.Exists(exePath))
                    {
                        percent.Report(100);
                        return exePath;
                    }

                    string target = Path.Combine(path, string.IsNullOrEmpty(dependency.ZipFilename)
                        ? dependency.ExeName
                        : dependency.ZipFilename);

                    Directory.CreateDirectory(path);

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

                    if (!string.IsNullOrEmpty(dependency.ZipFilename))
                    {
                        ZipFile.ExtractToDirectory(target, path);
                    }

                    if (!File.Exists(exePath))
                    {
                        throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
                    }
                }
                File.Delete(downloadLock);
            }
            percent.Report(100);
            return exePath;
        }

        private readonly ExternalDep _lz4 = new ExternalDep()
        {
            Name = "Lz4",
            Url = @"https://github.com/lz4/lz4/releases/download/v1.8.3/lz4_v1_8_3_win64.zip",
            ZipFilename = "lz4_v1_8_3_win64.zip",
            ExtractFolder = "lz4",
            ExeName = "lz4.exe"
        };

        private readonly ExternalDep _brotli = new ExternalDep()
        {
            Name = "Brotli",
            Url = @"https://github.com/google/brotli/releases/download/v1.0.4/brotli-v1.0.4-win_x86_64.zip",
            ZipFilename = "brotli-v1.0.4-win_x86_64.zip",
            ExtractFolder = "brotli",
            ExeName = "brotli.exe"
        };

        private readonly ExternalDep _zStd = new ExternalDep()
        {
            Name = "Zstandard",
            Url = @"https://github.com/facebook/zstd/releases/download/v1.3.5/zstd-v1.3.5-win64.zip",
            ZipFilename = "zstd-v1.3.5-win64.zip",
            ExtractFolder = "zstd",
            ExeName = "zstd.exe"
        };

        // From : https://bintray.com/vszakats/generic/openssl
        // referenced from official OpenSSL wiki : https://wiki.openssl.org/index.php/Binaries
        private readonly ExternalDep _openSsl = new ExternalDep()
        {
            Name = "OpenSSL",
            Url = @"https://bintray.com/vszakats/generic/download_file?file_path=openssl-1.1.1-win64-mingw.zip",
            ZipFilename = "openssl-1.1.1-win64-mingw.zip",
            ExtractFolder = "openssl",
            ExeName = "openssl-1.1.1-win64-mingw\\openssl.exe"
        };

    }
}