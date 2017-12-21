using System.IO;
using System.IO.Compression;
using System.Net;

namespace TCC
{
    public class ExternalDependecies
    {
		
        private static object _lock = new object();
        private const string ExeTar = @"C:\Program Files\Git\usr\bin\tar.exe";
        private const string ExeLz4 = @"lz4.exe";
        private const string ExeOpenSsl = @"C:\Program Files\Git\usr\bin\openssl.exe";

        public string Tar()
        {
			//https://github.com/git-for-windows/git-sdk-64/tree/master/mingw64/bin
			
            if (!File.Exists(ExeTar))
            {
                throw new FileNotFoundException("tar not found in " + ExeTar);
            }
            return ExeTar;
        }

        public string Lz4()
        {
            string root = Path.GetDirectoryName(typeof(ExternalDependecies).Assembly.CodeBase.Replace("file:///", ""));
            string exePath = Path.Combine(root, ExeLz4);
            if (!File.Exists(exePath))
            {
                lock (_lock)
                {
                    if (File.Exists(exePath))
                    {
                        return exePath;
                    }
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(@"https://github.com/lz4/lz4/releases/download/v1.8.0/lz4_v1_8_0_win64.zip", "lz4_v1_8_0_win64.zip");
                    }
                    ZipFile.ExtractToDirectory("lz4_v1_8_0_win64.zip", root);
                    if (!File.Exists(exePath))
                    {
                        throw new FileNotFoundException("lz4 not found in " + exePath);
                    }
                }
            }
            return exePath;
        }

        public string OpenSsl()
        {
            if (!File.Exists(ExeOpenSsl))
            {
                throw new FileNotFoundException("openssl not found in " + ExeTar);
            }
            return ExeOpenSsl;
        }
    }
}