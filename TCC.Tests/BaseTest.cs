using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;
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
        public async Task CompressDecompress(PasswordMode mode)
        {
            var e = new ExternalDependecies();
            await e.EnsureAllDependenciesPresent();

            string toCompressFolder = TestHelper.NewFolder();
            string compressedFolder = TestHelper.NewFolder();
            string decompressedFolder = TestHelper.NewFolder();
            string keysFolder = TestHelper.NewFolder();

            var data = TestData.CreateFiles(1, 1, toCompressFolder);
            var compressOption = data.GetTccCompressOption(compressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    compressOption.PasswordOption = new InlinePasswordOption() { Password = "1234" };
                    break;
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    TestHelper.FillFile(passfile, "123456");

                    compressOption.PasswordOption = new PasswordFileOption() { PasswordFile = passfile };
                    break;
                case PasswordMode.PublicKey:
                    {
                        await TestHelper.CreateKeyPairCommand("keypair.pem", KeySize.Key4096).Run(keysFolder, CancellationToken.None);
                        await TestHelper.CreatePublicKeyCommand("keypair.pem", "public.pem").Run(keysFolder, CancellationToken.None);
                        await TestHelper.CreatePrivateKeyCommand("keypair.pem", "private.pem").Run(keysFolder, CancellationToken.None);
                        compressOption.PasswordOption = new PublicKeyPasswordOption()
                        {
                            PublicKeyFile = Path.Combine(keysFolder, "public.pem")
                        };
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultCompress = await TarCompressCrypt.Compress(compressOption);

            Assert.True(resultCompress.IsSuccess);
            Assert.NotEmpty(resultCompress.Blocks);
            Assert.NotEmpty(resultCompress.CommandResults);

            var decomp = new TestData { Directories = new List<DirectoryInfo> { new DirectoryInfo(compressedFolder) } };

            var decompOption = decomp.GetTccDecompressOption(decompressedFolder);

            switch (mode)
            {
                case PasswordMode.None:
                    break;
                case PasswordMode.InlinePassword:
                    decompOption.PasswordOption = new InlinePasswordOption() { Password = "1234" };
                    break;
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    decompOption.PasswordOption = new PasswordFileOption() { PasswordFile = passfile };
                    break;
                case PasswordMode.PublicKey:
                    decompOption.PasswordOption = new PrivateKeyPasswordOption()
                    {
                        PrivateKeyFile = Path.Combine(keysFolder, "private.pem")
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            var resultDecompress = await TarCompressCrypt.Decompress(decompOption);
            Assert.True(resultDecompress.IsSuccess);
            Assert.NotEmpty(resultDecompress.Blocks);
            Assert.NotEmpty(resultDecompress.CommandResults);

            Console.WriteLine("TEST : src=" + toCompressFolder);
            Console.WriteLine("TEST : dst=" + decompressedFolder);

            FileInfo src = new DirectoryInfo(toCompressFolder).EnumerateFiles().FirstOrDefault();
            FileInfo dst = new DirectoryInfo(decompressedFolder).EnumerateFiles().FirstOrDefault();

            Assert.True(TestHelper.FilesAreEqual(src, dst));
        }

    }
}
