using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TCC.Lib.Blocks;
using TCC.Lib.Command;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib
{
    public class TarCompressCrypt
    {
        private readonly ExternalDependencies _externalDependencies;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IBlockListener _blockListener;
        private readonly ILogger<TarCompressCrypt> _logger;

        public TarCompressCrypt(ExternalDependencies externalDependencies, CancellationTokenSource cancellationTokenSource, IBlockListener blockListener, ILogger<TarCompressCrypt> logger)
        {
            _externalDependencies = externalDependencies;
            _cancellationTokenSource = cancellationTokenSource;
            _blockListener = blockListener;
            _logger = logger;
        }

        public Task<OperationSummary> Compress(CompressOption compressOption)
        {
            List<Block> blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            return ProcessingLoop(blocks, compressOption, Encrypt);
        }

        public Task<OperationSummary> Decompress(DecompressOption decompressOption)
        {
            List<Block> blocks = BlockHelper.PreprareDecompressBlocks(decompressOption).ToList();

            return ProcessingLoop(blocks, decompressOption, Decrypt);
        }

        private async Task<OperationSummary> ProcessingLoop(IList<Block> blocks,
            TccOption option,
            Func<Block, TccOption, Task<CommandResult>> processor)
        {
            var commandResults = new ConcurrentBag<CommandResult>();
            await blocks.ParallelizeAsync(async (b, token) =>
            {
                CommandResult result = null;
                try
                {
                    
                    _logger.LogInformation($"Starting {b.Source}");
                    result = await processor(b, option);
                    _blockListener.Add(new BlockReport(result, b, blocks.Count));
                    _logger.LogInformation($"Finished {b.Source} on {result.ElapsedMilliseconds} ms");
                }
                catch (Exception e)
                {
                    _logger.LogError(e,$"Error on {b.Source}");
                    if (result != null)
                    {
                        result.Errors += e.Message;
                    }
                }
                commandResults.Add(result);

            }, option.Threads, option.FailFast ? Fail.Fast : Fail.Smart, _cancellationTokenSource.Token);

            return new OperationSummary(blocks, commandResults);
        }

        private async Task<CommandResult> Encrypt(Block block, TccOption option)
        {
            var k = await PrepareEncryptionKey(block, option, _cancellationTokenSource.Token);

            string cmd = CompressCommand(block, option as CompressOption, _externalDependencies);
            var result = await cmd.Run(block.OperationFolder, _cancellationTokenSource.Token);

            await CleanupKey(block, option, k, result, Mode.Compress);

            return result;
        }

        private async Task<CommandResult> Decrypt(Block block, TccOption option)
        {
            var ext = new ExternalDependencies();

            var k = await PrepareDecryptionKey(block, option, _cancellationTokenSource.Token);

            string cmd = DecompressCommand(block, option, ext);
            var result = await cmd.Run(block.OperationFolder, _cancellationTokenSource.Token);

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

                var ext = new ExternalDependencies();
                // generate random passfile
                var passfile = await GenerateRandomKey(ext.OpenSsl(), key).Run(block.DestinationFolder, cancellationToken);
                passfile.ThrowOnError();
                // crypt passfile
                var cryptPass = await EncryptRandomKey(ext.OpenSsl(), key, keyCrypted, publicKey.PublicKeyFile).Run(block.DestinationFolder, cancellationToken);
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

        private static async Task<EncryptionKey> PrepareDecryptionKey(Block block, TccOption option, CancellationToken cancellationToken)
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
                    var ext = new ExternalDependencies();
                    await DecryptRandomKey(ext.OpenSsl(), key, keyCrypted, privateKey.PrivateKeyFile).Run(block.DestinationFolder, cancellationToken);
                    block.BlockPasswordFile = key;

                    break;
                case PasswordMode.PasswordFile when option.PasswordOption is PasswordFileOption passwordFile:
                    block.BlockPasswordFile = passwordFile.PasswordFile;
                    break;
            }
            return new EncryptionKey(key, keyCrypted);
        }

        private static string CompressCommand(Block block, CompressOption option, ExternalDependencies ext)
        {
            var cmd = new StringBuilder();
            string ratio;

            switch (option.Algo)
            {
                case CompressionAlgo.Lz4:
                    ratio = option.CompressionRatio != 0 ? $"-{option.CompressionRatio}" : string.Empty;
                    break;
                case CompressionAlgo.Brotli:
                    ratio = option.CompressionRatio != 0 ? $"-q {option.CompressionRatio}" : string.Empty;
                    break;
                case CompressionAlgo.Zstd:
                    ratio = option.CompressionRatio != 0 ? $"-{option.CompressionRatio}" : string.Empty;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
            }

            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    // tar -c C:\SourceFolder | lz4.exe -1 - compressed.tar.lz4
                    cmd.Append($"{ext.Tar()} -c {block.Source}");
                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {ext.Lz4()} {ratio} -v - {block.DestinationArchive}");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {ext.Brotli()} {ratio} - -o {block.DestinationArchive}");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {ext.Zstd()} {ratio} - -o {block.DestinationArchive}");
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
                    cmd.Append($"{ext.Tar()} -c {block.Source}");
                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {ext.Lz4()} {ratio} -v - ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {ext.Brotli()} {ratio} - ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {ext.Zstd()} {ratio} - ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {ext.OpenSsl()} aes-256-cbc {passwdCommand} -out {block.DestinationArchive}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd.ToString();
        }


        private static string DecompressCommand(Block block, TccOption option, ExternalDependencies ext)
        {
            var cmd = new StringBuilder();
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    //lz4 archive.tar.lz4 -dc --no-sparse | tar xf -
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($"{ext.Lz4()} {block.Source} -dc --no-sparse ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($"{ext.Brotli()} {block.Source} -d -c ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($"{ext.Zstd()} {block.Source} -d -c ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {ext.Tar()} xf - ");
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option, block);
                    //openssl aes-256-cbc -d -k "test" -in crypted.tar.lz4.aes | lz4 -dc --no-sparse - | tar xf -
                    cmd.Append($"{ext.OpenSsl()} aes-256-cbc -d {passwdCommand} -in {block.Source}");
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {ext.Lz4()} -dc --no-sparse - ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {ext.Brotli()} - -d ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {ext.Zstd()} - -d ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {ext.Tar()} xf - ");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd.ToString();
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

        private static string EncryptRandomKey(string openSslPath, string keyPath, string keyCryptedPath, string publicKey)
        {
            if (String.IsNullOrWhiteSpace(publicKey))
            {
                throw new CommandLineException("Asynmetric public key file missing");
            }
            return $"{openSslPath} rsautl -encrypt -inkey {publicKey} -pubin -in {keyPath} -out {keyCryptedPath}";
        }

        private static string DecryptRandomKey(string openSslPath, string keyPath, string keyCryptedPath, string privateKey)
        {
            if (String.IsNullOrWhiteSpace(privateKey))
            {
                throw new CommandLineException("Asynmetric private key file missing");
            }
            return $"{openSslPath} rsautl -decrypt -inkey {privateKey} -in {keyCryptedPath} -out {keyPath}";
        }

        private static string GenerateRandomKey(string openSslPath, string filename)
        {
            // 512 byte == 4096 bit
            return $"{openSslPath} rand -base64 256 > {filename}";
        }
    }
}