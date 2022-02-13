using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Options;

namespace TCC.Lib.Benchmark
{
    public class BenchmarkOptionHelper
    {
        private static Guid _pass = new Guid("ECEF7408-4D58-4776-98FE-E0ED604C2D7C");
        private readonly ExternalDependencies _externalDependencies;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public BenchmarkOptionHelper(ExternalDependencies externalDependencies, CancellationTokenSource cancellationTokenSource)
        {
            _externalDependencies = externalDependencies;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public PasswordOption GenerateDecompressPasswordOption(PasswordMode passwordMode, string keysFolder)
        {
            switch (passwordMode)
            {
                case PasswordMode.None:
                    return NoPasswordOption.Nop;
                case PasswordMode.InlinePassword:
                    return new InlinePasswordOption { Password = _pass.ToString("N") };
                case PasswordMode.PasswordFile:
                    return new PasswordFileOption { PasswordFile = Path.Combine(keysFolder, "password.txt") };
                case PasswordMode.PublicKey:
                    return new PrivateKeyPasswordOption
                    {
                        PrivateKeyFile = Path.Combine(keysFolder, "private.pem")
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(passwordMode), passwordMode, null);
            }
        }

        public async Task<PasswordOption> GenerateCompressPasswordOption(PasswordMode passwordMode, string keysFolder)
        {
            switch (passwordMode)
            {
                case PasswordMode.None:
                    return NoPasswordOption.Nop;
                case PasswordMode.InlinePassword:
                    return new InlinePasswordOption { Password = _pass.ToString("N") };
                case PasswordMode.PasswordFile:
                    string passwordFile = Path.Combine(keysFolder, "password.txt");
                    TestFileHelper.FillFile(passwordFile, "123456");
                    return new PasswordFileOption { PasswordFile = passwordFile };
                case PasswordMode.PublicKey:
                    {
                        var e = _externalDependencies;
                        var keyPair = await CreateKeyPairCommand(e.OpenSsl(), "keypair.pem", KeySize.Key4096).Run(keysFolder, _cancellationTokenSource.Token);
                        var publicKey = await CreatePublicKeyCommand(e.OpenSsl(), "keypair.pem", "public.pem").Run(keysFolder, _cancellationTokenSource.Token);
                        var privateKey = await CreatePrivateKeyCommand(e.OpenSsl(), "keypair.pem", "private.pem").Run(keysFolder, _cancellationTokenSource.Token);

                        keyPair.ThrowOnError();
                        publicKey.ThrowOnError();
                        privateKey.ThrowOnError();

                        return new PublicKeyPasswordOption
                        {
                            PublicKeyFile = Path.Combine(keysFolder, "public.pem")
                        };
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(passwordMode), passwordMode, null);
            }
        }

        public string CreateKeyPairCommand(string openSslPath, string keyPairFile, KeySize keySize)
            => $"{openSslPath} genpkey -quiet -algorithm RSA -out {keyPairFile} -pkeyopt rsa_keygen_bits:{(int)keySize}";

        public string CreatePublicKeyCommand(string openSslPath, string keyPairFile, string publicKeyFile)
            => $"{openSslPath} rsa -pubout -outform PEM -in {keyPairFile} -out {publicKeyFile}";

        public string CreatePrivateKeyCommand(string openSslPath, string keyPairFile, string privateKeyFile)
            => $"{openSslPath} rsa -outform PEM -in {keyPairFile} -out {privateKeyFile}";

    }
}