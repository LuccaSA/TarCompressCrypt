using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Command;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public static class BenchmarkOptionHelper
    {

        public static PasswordOption GenerateDecompressPasswordOption(PasswordMode passwordMode, string keysFolder)
        {
            switch (passwordMode)
            {
                case PasswordMode.None:
                    return NoPasswordOption.Nop;
                case PasswordMode.InlinePassword:
                    return new InlinePasswordOption { Password = "1234" };
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    return new PasswordFileOption { PasswordFile = passfile };
                case PasswordMode.PublicKey:
                    return new PrivateKeyPasswordOption
                    {
                        PrivateKeyFile = Path.Combine(keysFolder, "private.pem")
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(passwordMode), passwordMode, null);
            }
        }

        public static async Task<PasswordOption> GenerateCompressPassswordOption(PasswordMode passwordMode, string keysFolder)
        {
            switch (passwordMode)
            {
                case PasswordMode.None:
                    return NoPasswordOption.Nop;
                case PasswordMode.InlinePassword:
                    return new InlinePasswordOption { Password = "1234" };
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    TestFileHelper.FillFile(passfile, "123456");
                    return new PasswordFileOption { PasswordFile = passfile };
                case PasswordMode.PublicKey:
                {
                    await TestFileHelper.CreateKeyPairCommand("keypair.pem", KeySize.Key4096).Run(keysFolder, CancellationToken.None);
                    await TestFileHelper.CreatePublicKeyCommand("keypair.pem", "public.pem").Run(keysFolder, CancellationToken.None);
                    await TestFileHelper.CreatePrivateKeyCommand("keypair.pem", "private.pem").Run(keysFolder, CancellationToken.None);
                    return new PublicKeyPasswordOption
                    {
                        PublicKeyFile = Path.Combine(keysFolder, "public.pem")
                    };
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(passwordMode), passwordMode, null);
            }
        }
    }
}