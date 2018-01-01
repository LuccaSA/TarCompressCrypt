using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using Xunit;

namespace TCC.Tests
{
    public class CompressTest
    {
        [Theory]
        [InlineData(PasswordMode.None)]
        [InlineData(PasswordMode.InlinePassword)]
        [InlineData(PasswordMode.PasswordFile)]
        [InlineData(PasswordMode.PublicKey)]
        public void CompressDecompress(PasswordMode mode)
        {
            string toCompressFolder = TestHelper.NewFolder();
            string compressedFolder = TestHelper.NewFolder();
            string decompressedFolder = TestHelper.NewFolder();
            string keysFolder = TestHelper.NewFolder();

            var data = TestData.CreateFiles(1, 1, toCompressFolder);
            var compressOption = data.GetTccCompressOption(mode, compressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    compressOption.Password = "1234";
                    break;
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    TestHelper.FillFile(passfile, "123456");
                    compressOption.PasswordFile = passfile;
                    break;
                case PasswordMode.PublicKey:
                    {
                        var p1 = TestHelper.CreateKeyPairCommand("keypair.pem", KeySize.Key4096).Run(keysFolder, CancellationToken.None);
                        var p2 = TestHelper.CreatePublicKeyCommand("keypair.pem", "public.pem").Run(keysFolder, CancellationToken.None);
                        var p3 = TestHelper.CreatePrivateKeyCommand("keypair.pem", "private.pem").Run(keysFolder, CancellationToken.None);
                        compressOption.PublicPrivateKeyFile = Path.Combine(keysFolder, "public.pem");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultCompress = TarCompressCrypt.Compress(compressOption);

            Assert.Equal(0, resultCompress);

            var decomp = new TestData { Directories = new List<DirectoryInfo> { new DirectoryInfo(compressedFolder) } };

            var decompOption = decomp.GetTccDecompressOption(mode, decompressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    decompOption.Password = "1234";
                    break;
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    decompOption.PasswordFile = passfile;
                    break;
                case PasswordMode.PublicKey:
                    decompOption.PublicPrivateKeyFile = Path.Combine(keysFolder, "private.pem");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultDecompress = TarCompressCrypt.Decompress(decompOption);
            Assert.Equal(0, resultDecompress);

            Console.WriteLine("TEST : src=" + toCompressFolder);
            Console.WriteLine("TEST : dst=" + decompressedFolder);

            FileInfo src = new DirectoryInfo(toCompressFolder).EnumerateFiles().FirstOrDefault();
            FileInfo dst = new DirectoryInfo(decompressedFolder).EnumerateFiles().FirstOrDefault();

            Assert.True(TestHelper.FilesAreEqual(src, dst));
        }

    }
}
