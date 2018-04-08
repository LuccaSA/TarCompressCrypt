using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace TCC.Lib.Dependencies
{
    public class ExternalDependecies
    {
        private const string ExeTar = @"Libs\tar.exe";
        private const string ExeOpenSsl = @"Libs\openssl.exe";
        private const string CnfOpenSsl = @"Libs\openssl.cnf";

        public string Tar()
        {
            string tarPath = Path.Combine(Root, ExeTar);
            if (!File.Exists(tarPath))
            {
                throw new FileNotFoundException("tar not found in " + tarPath);
            }
            return tarPath;
        }

        public string OpenSsl()
        {
            EnsureOpenSslConfigurationPath();
            string openSslPath = Path.Combine(Root, ExeOpenSsl);
            if (!File.Exists(openSslPath))
            {
                throw new FileNotFoundException("openssl not found in " + openSslPath);
            }
            return openSslPath;
        }

        private void EnsureOpenSslConfigurationPath()
        {
            var openSslCnfPath = Environment.GetEnvironmentVariable("OPENSSL_CONF", EnvironmentVariableTarget.Process);
            if (string.IsNullOrWhiteSpace(openSslCnfPath))
            {
                string openSslConfPath = Path.Combine(Root, CnfOpenSsl);
                Environment.SetEnvironmentVariable("OPENSSL_CONF", openSslConfPath, EnvironmentVariableTarget.Process);
            }
        }

        public string Lz4() => GetPath(_lz4);

        public async Task EnsureAllDependenciesPresent()
        {
            var pLz4 = new Progress<int>();
            await EnsureDependency(_lz4, pLz4);
        }

        public string Root => Path.GetDirectoryName(typeof(ExternalDependecies).Assembly.CodeBase.Replace("file:///", ""));

        public string GetPath(ExternalDep dependency)
        {
            string exePath = Path.Combine(Root, dependency.ExtractFolder, dependency.ExeName);
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException(dependency.Name + " not found in " + exePath);
            }
            return exePath;
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
                        await wc.DownloadFileTaskAsync(dependency.URL, target);
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
            URL = @"https://github.com/lz4/lz4/releases/download/v1.8.0/lz4_v1_8_0_win64.zip",
            ZipFilename = "lz4_v1_8_0_win64.zip",
            ExtractFolder = "lz4",
            ExeName = "lz4.exe"
        };
         
    }
}