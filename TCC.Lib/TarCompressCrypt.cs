using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib
{
    public static class TarCompressCrypt
    {
        public static Task<OperationSummary> Compress(CompressOption compressOption, BlockingCollection<(CommandResult Cmd, Block Block, int Total)> obervableLog = null, CancellationToken cancellationToken = default)
        {
            List<Block> blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            return ProcessingLoop(blocks, compressOption, Encrypt, obervableLog, cancellationToken);
        }

        public static Task<OperationSummary> Decompress(DecompressOption decompressOption, BlockingCollection<(CommandResult Cmd, Block Block, int Total)> obervableLog = null, CancellationToken cancellationToken = default)
        {
            List<Block> blocks = BlockHelper.PreprareDecompressBlocks(decompressOption).ToList();

            return ProcessingLoop(blocks, decompressOption, Decrypt, obervableLog, cancellationToken);
        }

        private static async Task<OperationSummary> ProcessingLoop(IList<Block> blocks,
            TccOption option,
            Func<Block, TccOption, CancellationToken, Task<CommandResult>> processor,
            BlockingCollection<(CommandResult Cmd, Block Block, int Total)> obervableLog = null,
            CancellationToken cancellationToken = default)
        {
            var commandResults = new ConcurrentBag<CommandResult>();
            var parallelized = await blocks.ParallelizeAsync(async (b, token) =>
             {
                 CommandResult result = null;
                 try
                 {
                     result = await processor(b, option, cancellationToken);
                     obervableLog?.Add((result, b, blocks.Count));
                 }
                 catch (Exception e)
                 {
                     if (result != null)
                     {
                         result.Errors += e.Message;
                     }
                 }
                 commandResults.Add(result);

             }, option.Threads, option.FailFast ? Fail.Fast : Fail.Smart, cancellationToken);

            obervableLog?.CompleteAdding();

            return new OperationSummary(blocks, commandResults, parallelized.IsSucess);
        }

        private static async Task<CommandResult> Encrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();

            var k = await PrepareEncryptionKey(block, option, cancellationToken);

            string cmd = CompressCommand(block, option as CompressOption, ext);
            var result = await cmd.Run(block.OperationFolder, cancellationToken);

            await CleanupKey(block, option, k, result, Mode.Compress);

            return result;
        }

        private static async Task<CommandResult> Decrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();

            var k = await PrepareDecryptionKey(block, option, cancellationToken);

            string cmd = DecompressCommand(block, option, ext);
            var result = await cmd.Run(block.OperationFolder, cancellationToken);

            await CleanupKey(block, option, k, result, Mode.Compress);

            return result;
        }

        private static Task CleanupKey(Block block, TccOption option, EncryptionKey key, CommandResult result, Mode mode)
        {
            if (option.PasswordOption.PasswordMode == PasswordMode.PublicKey)
            {
                // delete uncrypted pass
                if (!String.IsNullOrEmpty(key.KeyCrypted))
                {
                    return Path.Combine(block.DestinationFolder, key.Key).TryDeleteFileWithRetryAsync();
                }
                // if error in compression, also delete encrypted passfile
                if (mode == Mode.Compress && result.HasError && !String.IsNullOrEmpty(key.KeyCrypted))
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

        private static async Task<EncryptionKey> PrepareEncryptionKey(Block block, TccOption option, CancellationToken cancellationToken)
        {
            string key = null;
            string keyCrypted = null;

            if (option.PasswordOption.PasswordMode == PasswordMode.PublicKey &&
                option.PasswordOption is PublicKeyPasswordOption publicKey)
            {
                key = block.ArchiveName + ".key";
                keyCrypted = block.ArchiveName + ".key.encrypted";
                // generate random passfile
                await GenerateRandomKey(key).Run(block.DestinationFolder, cancellationToken);
                // crypt passfile
                await EncryptRandomKey(key, keyCrypted, publicKey.PublicKeyFile).Run(block.DestinationFolder, cancellationToken);

                block.BlockPasswordFile = Path.Combine(block.DestinationFolder, key);
            }
            else if (option.PasswordOption.PasswordMode == PasswordMode.PasswordFile &&
                     option.PasswordOption is PasswordFileOption passwordFile)
            {
                block.BlockPasswordFile = passwordFile.PasswordFile;
            }

            return new EncryptionKey(key, keyCrypted);
        }

        private static async Task<EncryptionKey> PrepareDecryptionKey(Block block, TccOption option, CancellationToken cancellationToken)
        {
            string key = null;
            string keyCrypted = null;
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.PublicKey when option.PasswordOption is PrivateKeyPasswordOption privateKey:

                    var file = new FileInfo(block.ArchiveName);
                    var dir = file.Directory.FullName;
                    var name = file.Name.Substring(0, file.Name.IndexOf(".tar", StringComparison.InvariantCultureIgnoreCase));
                    keyCrypted = Path.Combine(dir, name + ".key.encrypted");
                    key = Path.Combine(dir, name + ".key");

                    await DecryptRandomKey(key, keyCrypted, privateKey.PrivateKeyFile).Run(block.DestinationFolder, cancellationToken);
                    block.BlockPasswordFile = key;

                    break;
                case PasswordMode.PasswordFile when option.PasswordOption is PasswordFileOption passwordFile:
                    block.BlockPasswordFile = passwordFile.PasswordFile;
                    break;
            }
            return new EncryptionKey(key, keyCrypted);
        }

        private static string CompressCommand(Block block, CompressOption option, ExternalDependecies ext)
        {
            string cmd;
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    // tar -c C:\SourceFolder | lz4.exe -1 - compressed.tar.lz4
                    cmd = $"{ext.Tar().Escape()} -c {block.Source.Escape()}";

                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd += $" | {ext.Lz4().Escape()} -1 -v - {block.DestinationArchive.Escape()}";
                            break;
                        case CompressionAlgo.Brotli:
                            cmd += $" | {ext.Brotli().Escape()} - -o {block.DestinationArchive.Escape()}";
                            break;
                        case CompressionAlgo.Zstd:
                            cmd += $" | {ext.Zstd().Escape()} - -o {block.DestinationArchive.Escape()}";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
                    }
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option, block);
                    // tar -c C:\SourceFolder | lz4.exe -1 - | openssl aes-256-cbc -k "password" -out crypted.tar.lz4.aes
                    cmd = $"{ext.Tar().Escape()} -c {block.Source.Escape()}";
                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd += $" | {ext.Lz4().Escape()} -1 -v - ";
                            break;
                        case CompressionAlgo.Brotli:
                            cmd += $" | {ext.Brotli().Escape()} - ";
                            break;
                        case CompressionAlgo.Zstd:
                            cmd += $" | {ext.Zstd().Escape()} - ";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
                    }

                    cmd += $" | {ext.OpenSsl().Escape()} aes-256-cbc {passwdCommand} -out {block.DestinationArchive.Escape()}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd;
        }


        private static string DecompressCommand(Block block, TccOption option, ExternalDependecies ext)
        {
            string cmd = null;
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    //lz4 archive.tar.lz4 -dc --no-sparse | tar xf -
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd = $"{ext.Lz4().Escape()} {block.Source.Escape()} -dc --no-sparse ";
                            break;
                        case CompressionAlgo.Brotli:
                            cmd = $"{ext.Brotli().Escape()} {block.Source.Escape()} -d -c ";
                            break;
                        case CompressionAlgo.Zstd:
                            cmd = $"{ext.Zstd().Escape()} {block.Source.Escape()} -d -c ";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd += $" | {ext.Tar().Escape()} xf - ";
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option, block);
                    //openssl aes-256-cbc -d -k "test" -in crypted.tar.lz4.aes | lz4 -dc --no-sparse - | tar xf -
                    cmd = $"{ext.OpenSsl().Escape()} aes-256-cbc -d {passwdCommand} -in {block.Source}";
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd += $" | {ext.Lz4().Escape()} -dc --no-sparse - ";
                            break;
                        case CompressionAlgo.Brotli:
                            cmd += $" | {ext.Brotli().Escape()} - -d ";
                            break;
                        case CompressionAlgo.Zstd:
                            cmd += $" | {ext.Zstd().Escape()} - -d ";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd += $" | {ext.Tar().Escape()} xf - ";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd;
        }

        private static string PasswordCommand(TccOption option, Block block)
        {
            string passwdCommand;
            if (option.PasswordOption.PasswordMode == PasswordMode.InlinePassword
                && option.PasswordOption is InlinePasswordOption inlinePassword)
            {
                if (String.IsNullOrWhiteSpace(inlinePassword.Password))
                {
                    throw new CommandLineException("Password missing");
                }
                passwdCommand = "-k " + inlinePassword.Password;
            }
            else
            {
                if (String.IsNullOrWhiteSpace(block.BlockPasswordFile))
                {
                    throw new CommandLineException("Password file missing");
                }
                passwdCommand = "-kfile " + block.BlockPasswordFile;
            }

            return passwdCommand;
        }

        private static string EncryptRandomKey(string keyPath, string keyCryptedPath, string publicKey)
        {
            if (String.IsNullOrWhiteSpace(publicKey))
            {
                throw new CommandLineException("Asynmetric public key file missing");
            }
            return $"openssl rsautl -encrypt -inkey {publicKey} -pubin -in {keyPath} -out {keyCryptedPath}";
        }

        private static string DecryptRandomKey(string keyPath, string keyCryptedPath, string privateKey)
        {
            if (String.IsNullOrWhiteSpace(privateKey))
            {
                throw new CommandLineException("Asynmetric private key file missing");
            }
            return $"openssl rsautl -decrypt -inkey {privateKey} -in {keyCryptedPath} -out {keyPath}";
        }

        private static string GenerateRandomKey(string filename)
        {
            // 512 byte == 4096 bit
            return $"openssl rand -base64 256 > {filename}";
        }

        //"C:\Program Files\Git\usr\bin\find.exe" C:\Sites\lucca\ -mmin -5760 -type f

    }
}