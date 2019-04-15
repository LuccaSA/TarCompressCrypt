using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private CompressionCommands _compressionCommands;

        public EncryptionCommands(ExternalDependencies externalDependencies, CompressionCommands compressionCommands)
        {
            _ext = externalDependencies;
            _compressionCommands = compressionCommands;
        }

        public async Task<CommandResult> Encrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            EncryptionKey k = null;
            CommandResult result = null;
            try
            { 
                k = await PrepareEncryptionKey(block, option, cancellationToken);
                 
                string cmd = _compressionCommands.CompressCommand(block, option as CompressOption);
                result = await cmd.Run(block.OperationFolder, cancellationToken);
            }
            finally
            {
                await CleanupKey(block, option, k, result, Mode.Compress);
            }
            return result;
        }

        public async Task<CommandResult> Decrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            EncryptionKey k = null;
            CommandResult result = null;
            try
            { 
                k = await PrepareDecryptionKey(block, option, cancellationToken);
                 
                string cmd = _compressionCommands.DecompressCommand(block, option);
                result = await cmd.Run(block.OperationFolder, cancellationToken);
            }
            finally
            {
                await CleanupKey(block, option, k, result, Mode.Compress);
            }
            return result;
        }

        private static Task CleanupKey(Block block, TccOption option, EncryptionKey key, CommandResult result, Mode mode)
        {
            if (key == null)
            {
                return Task.CompletedTask;
            }
            if (option.PasswordOption.PasswordMode == PasswordMode.PublicKey)
            {
                // delete uncrypted pass
                if (!String.IsNullOrEmpty(key.KeyCrypted))
                {
                    return Path.Combine(block.DestinationFolder, key.Key).TryDeleteFileWithRetryAsync();
                }
                // if error in compression, also delete encrypted passfile
                if (mode == Mode.Compress && (result == null || result.HasError) && !String.IsNullOrEmpty(key.KeyCrypted))
                {
                    return Path.Combine(block.DestinationFolder, key.KeyCrypted).TryDeleteFileWithRetryAsync();
                }
            }
            return Task.CompletedTask;
        }

        public class EncryptionKey
        {
            public EncryptionKey(string key, string keyCrypted)
            {
                Key = key;
                KeyCrypted = keyCrypted;
            }

            public string Key { get; }
            public string KeyCrypted { get; }
        }

        private async Task<EncryptionKey> PrepareEncryptionKey(Block block, TccOption option, CancellationToken cancellationToken)
        {
            string key = null;
            string keyCrypted = null;

            if (option.PasswordOption.PasswordMode == PasswordMode.PublicKey &&
                option.PasswordOption is PublicKeyPasswordOption publicKey)
            {
                key = block.ArchiveName + ".key";
                keyCrypted = block.ArchiveName + ".key.encrypted";

                // generate random passfile
                var passfile = await GenerateRandomKey(_ext.OpenSsl(), key).Run(block.DestinationFolder, cancellationToken);
                passfile.ThrowOnError();
                // crypt passfile
                var cryptPass = await EncryptRandomKey(_ext.OpenSsl(), key, keyCrypted, publicKey.PublicKeyFile).Run(block.DestinationFolder, cancellationToken);
                cryptPass.ThrowOnError();

                block.BlockPasswordFile = Path.Combine(block.DestinationFolder, key);
            }
            else if (option.PasswordOption.PasswordMode == PasswordMode.PasswordFile &&
                     option.PasswordOption is PasswordFileOption passwordFile)
            {
                block.BlockPasswordFile = passwordFile.PasswordFile;
            }

            return new EncryptionKey(key, keyCrypted);
        }

        private async Task<EncryptionKey> PrepareDecryptionKey(Block block, TccOption option, CancellationToken cancellationToken)
        {
            string key = null;
            string keyCrypted = null;
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.PublicKey when option.PasswordOption is PrivateKeyPasswordOption privateKey:

                    var file = new FileInfo(block.ArchiveName);
                    var dir = file.Directory?.FullName;
                    var name = file.Name.Substring(0, file.Name.IndexOf(".tar", StringComparison.InvariantCultureIgnoreCase));
                    keyCrypted = Path.Combine(dir, name + ".key.encrypted");
                    key = Path.Combine(dir, name + ".key");

                    await DecryptRandomKey(_ext.OpenSsl(), key, keyCrypted, privateKey.PrivateKeyFile).Run(block.DestinationFolder, cancellationToken);
                    block.BlockPasswordFile = key;

                    break;
                case PasswordMode.PasswordFile when option.PasswordOption is PasswordFileOption passwordFile:
                    block.BlockPasswordFile = passwordFile.PasswordFile;
                    break;
            }
            return new EncryptionKey(key, keyCrypted);
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
