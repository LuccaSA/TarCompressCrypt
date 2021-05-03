using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Dependencies
{
    public class EncryptionCommands
    {
        private readonly ExternalDependencies _ext;

        public EncryptionCommands(ExternalDependencies externalDependencies)
        {
            _ext = externalDependencies;
        }

        public Task CleanupKey(Block block, TccOption option, Mode mode)
        {
            if (block.EncryptionKey?.Key == null || block.EncryptionKey?.KeyCrypted == null)
            {
                return Task.CompletedTask;
            }

            if (option.PasswordOption.PasswordMode != PasswordMode.PublicKey)
            {
                return Task.CompletedTask;
            }

            // delete uncrypted pass
            if (!String.IsNullOrEmpty(block.EncryptionKey.KeyCrypted))
            {
                return Path.Combine(block.ArchiveFolder.FullName, block.EncryptionKey.Key).TryDeleteFileWithRetryAsync();
            }
            // if error in compression, also delete encrypted passfile
            if (mode == Mode.Compress && !String.IsNullOrEmpty(block.EncryptionKey.KeyCrypted))
            {
                return Path.Combine(block.ArchiveFolder.FullName, block.EncryptionKey.KeyCrypted).TryDeleteFileWithRetryAsync();
            }
            return Task.CompletedTask;
        }

        public async Task PrepareEncryptionKey(CompressionBlock block, TccOption option, CancellationToken cancellationToken)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }
            string key = null;
            string keyCrypted = null;

            if (option.PasswordOption.PasswordMode == PasswordMode.PublicKey &&
                option.PasswordOption is PublicKeyPasswordOption publicKey)
            {
                key = block.DestinationArchiveName + ".key";
                keyCrypted = block.DestinationArchiveName + ".key.encrypted";

                // generate random passfile
                var passfile = await GenerateRandomKey(_ext.OpenSsl(), key).Run(block.DestinationArchiveFolder.FullName, cancellationToken);
                passfile.ThrowOnError();
                // crypt passfile
                var cryptPass = await EncryptRandomKey(_ext.OpenSsl(), key, keyCrypted, publicKey.PublicKeyFile)
                    .Run(block.DestinationArchiveFolder.FullName, cancellationToken);

                cryptPass.ThrowOnError();

                block.BlockPasswordFile = Path.Combine(block.DestinationArchiveFolder.FullName, key);
            }
            else if (option.PasswordOption.PasswordMode == PasswordMode.PasswordFile &&
                     option.PasswordOption is PasswordFileOption passwordFile)
            {
                block.BlockPasswordFile = passwordFile.PasswordFile;
            }

            block.EncryptionKey = new EncryptionKey(key, keyCrypted);
        }

        public async Task PrepareDecryptionKey(DecompressionBlock block, TccOption option, CancellationToken cancellationToken)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }
            string key = null;
            string keyCrypted = null;
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.PublicKey when option.PasswordOption is PrivateKeyPasswordOption privateKey:

                    var file = block.Archive;
                    var dir = file.Directory?.FullName;
                    var name = file.Name.Substring(0, file.Name.IndexOf(".tar", StringComparison.InvariantCultureIgnoreCase));
                    keyCrypted = Path.Combine(dir, name + ".key.encrypted");
                    key = Path.Combine(dir, name + ".key");

                    var result = await DecryptRandomKey(_ext.OpenSsl(), key, keyCrypted, privateKey.PrivateKeyFile)
                        .Run(block.ArchiveFolder.FullName, cancellationToken);

                    result.ThrowOnError();

                    block.BlockPasswordFile = key;

                    break;
                case PasswordMode.PasswordFile when option.PasswordOption is PasswordFileOption passwordFile:
                    block.BlockPasswordFile = passwordFile.PasswordFile;
                    break;
            }
            block.EncryptionKey = new EncryptionKey(key, keyCrypted);
        }

        private static string EncryptRandomKey(string openSslPath, string keyPath, string keyCryptedPath, string publicKey)
        {
            if (String.IsNullOrWhiteSpace(publicKey))
            {
                throw new CommandLineException("Asymmetric public key file missing");
            }
            return $"{openSslPath} rsautl -encrypt -inkey {publicKey.Escape()} -pubin -in {keyPath.Escape()} -out {keyCryptedPath.Escape()}";
        }

        private static string DecryptRandomKey(string openSslPath, string keyPath, string keyCryptedPath, string privateKey)
        {
            if (String.IsNullOrWhiteSpace(privateKey))
            {
                throw new CommandLineException("Asymmetric private key file missing");
            }
            return $"{openSslPath} rsautl -decrypt -inkey {privateKey.Escape()} -in {keyCryptedPath.Escape()} -out {keyPath.Escape()}";
        }

        private static string GenerateRandomKey(string openSslPath, string filename)
        {
            // 512 byte == 4096 bit
            return $"{openSslPath} rand -base64 256 > {filename.Escape()}";
        }
    }
}
