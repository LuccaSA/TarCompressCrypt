using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public static class BenchmarkOptionHelper
    {
        private  static Guid _pass = new Guid("ECEF7408-4D58-4776-98FE-E0ED604C2D7C");

        public static PasswordOption GenerateDecompressPasswordOption(PasswordMode passwordMode, string keysFolder)
        {
            switch (passwordMode)
            {
                case PasswordMode.None:
                    return NoPasswordOption.Nop;
                case PasswordMode.InlinePassword:
                    return new InlinePasswordOption { Password = _pass.ToString("N") };
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
                    return new InlinePasswordOption { Password = _pass.ToString("N") };
                case PasswordMode.PasswordFile:
                    string passfile = Path.Combine(keysFolder, "password.txt");
                    TestFileHelper.FillFile(passfile, "123456");
                    return new PasswordFileOption { PasswordFile = passfile };
                case PasswordMode.PublicKey:
                    {
                        var e = new ExternalDependencies();
                        await e.EnsureAllDependenciesPresent();
                        var kp = await CreateKeyPairCommand(e.OpenSsl(), "keypair.pem", KeySize.Key4096).Run(keysFolder, CancellationToken.None);
                        var pub = await CreatePublicKeyCommand(e.OpenSsl(), "keypair.pem", "public.pem").Run(keysFolder, CancellationToken.None);
                        var priv = await CreatePrivateKeyCommand(e.OpenSsl(), "keypair.pem", "private.pem").Run(keysFolder, CancellationToken.None);

                        kp.ThrowOnError();
                        pub.ThrowOnError();
                        priv.ThrowOnError();

                        return new PublicKeyPasswordOption
                        {
                            PublicKeyFile = Path.Combine(keysFolder, "public.pem")
                        };
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(passwordMode), passwordMode, null);
            }
        }

        public static string CreateKeyPairCommand(string openSslPath, string keyPairFile, KeySize keySize)
            => $"{openSslPath} genpkey -algorithm RSA -out {keyPairFile} -pkeyopt rsa_keygen_bits:{(int)keySize}";

        public static string CreatePublicKeyCommand(string openSslPath, string keyPairFile, string publicKeyFile)
            => $"{openSslPath} rsa -pubout -outform PEM -in {keyPairFile} -out {publicKeyFile}";

        public static string CreatePrivateKeyCommand(string openSslPath, string keyPairFile, string privateKeyFile)
            => $"{openSslPath} rsa -outform PEM -in {keyPairFile} -out {privateKeyFile}";

    }
}