using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TCC
{
    public static class TarCompressCrypt
    {
        private const string Pipe = @" | ";

        public static int Compress(CompressOption compressOption, BlockingCollection<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Block> blocks = BlockHelper.PreprareCompressBlocks(compressOption.SourceDirOrFile, compressOption.DestinationDir, compressOption.BlockMode, compressOption.PasswordMode != PasswordMode.None);

            return ProcessingLoop(blocks, compressOption, Encrypt, obervableLog, cancellationToken);
        }

        public static int Decompress(DecompressOption decompressOption, BlockingCollection<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Block> blocks = BlockHelper.PreprareDecompressBlocks(decompressOption.SourceDirOrFile, decompressOption.DestinationDir, decompressOption.PasswordMode != PasswordMode.None);

            return ProcessingLoop(blocks, decompressOption, Decrypt, obervableLog, cancellationToken);
        }

        private static int ProcessingLoop(IList<Block> blocks,
            TccOption option,
            Func<Block, TccOption, CancellationToken, CommandResult> processor,
            BlockingCollection<CommandResult> obervableLog = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ParallelOptions po = ParallelOptions(option.Threads); 

            var pr = Parallel.ForEach(Partitioner.Create(blocks, true), po, (b, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                CommandResult result = null;
                try
                {
                    result = processor(b, option, cancellationToken);
                    result.Block = b;
                    result.BatchTotal = blocks.Count;
                    obervableLog?.Add(result, cancellationToken);

                    if (result.HasError && option.FailFast)
                    {
                        state.Break();
                    }
                }
                catch (Exception e)
                {
                    if (option.FailFast)
                    {
                        if (result != null)
                        {
                            result.Errors += e.Message;
                        }
                        state.Break();
                        throw;
                    }
                }
            });

            obervableLog?.CompleteAdding();

            return pr.IsCompleted ? 0 : 1;
        }

        private static ParallelOptions ParallelOptions(string threads)
        {
            var nbThread = 1;
            if (!string.IsNullOrEmpty(threads))
            {
                if (string.Equals(threads, "all", StringComparison.InvariantCultureIgnoreCase))
                {
                    nbThread = Environment.ProcessorCount;
                }
                else if (!int.TryParse(threads, out nbThread))
                {
                    nbThread = 1;
                }
            }

            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = nbThread
            };
            return po;
        }

        private static CommandResult Encrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();

            PrepareEncryptionKey(block, option, out var key, out var keyCrypted, cancellationToken);

            string cmd = CompressCommand(block, option, ext);
            var result = cmd.Run(block.OperationFolder, cancellationToken);

            CleanupKey(block, option, key, keyCrypted, result, Mode.Compress);

            return result;
        }

        private static CommandResult Decrypt(Block block, TccOption option, CancellationToken cancellationToken)
        { 
            var ext = new ExternalDependecies();

            PrepareDecryptionKey(block, option, out var key, out var keyCrypted, cancellationToken);

            string cmd = DecompressCommand(block, option, ext);
            var result = cmd.Run(block.OperationFolder, cancellationToken);

            CleanupKey(block, option, key, keyCrypted, result, Mode.Compress);

            return result;
        }

        private static void CleanupKey(Block block, TccOption option, string key, string keyCrypted, CommandResult result, Mode mode)
        {
            if (option.PasswordMode == PasswordMode.PublicKey)
            {
                // delete uncrypted pass
                if (!String.IsNullOrEmpty(key))
                {
                    File.Delete(Path.Combine(block.DestinationFolder, key));
                }
                // if error in compression, also delete encrypted passfile
                if (mode == Mode.Compress && result.HasError && !String.IsNullOrEmpty(keyCrypted))
                {
                    File.Delete(Path.Combine(block.DestinationFolder, keyCrypted));
                }
            }
        }

        private static void PrepareEncryptionKey(Block block, TccOption option, out string key, out string keyCrypted, CancellationToken cancellationToken)
        {
            key = null;
            keyCrypted = null;
            if (option.PasswordMode == PasswordMode.PublicKey)
            {
                key = block.ArchiveName + ".key";
                keyCrypted = block.ArchiveName + ".key.encrypted";
                // generate random passfile
                var keyResult = GenerateRandomKey(key).Run(block.DestinationFolder, cancellationToken);
                // crypt passfile
                var keyCryptedResult = EncryptRandomKey(key, keyCrypted, option.PublicPrivateKeyFile).Run(block.DestinationFolder, cancellationToken);
                option.PasswordFile = Path.Combine(block.DestinationFolder, key);
            }
        }

        private static void PrepareDecryptionKey(Block block, TccOption option, out string key, out string keyCrypted, CancellationToken cancellationToken)
        {
            key = null;
            keyCrypted = null;
            if (option.PasswordMode == PasswordMode.PublicKey)
            {
                key = block.ArchiveName + ".key";
                keyCrypted = block.ArchiveName + ".key.encrypted";

                var file = new FileInfo(block.ArchiveName);
                var dir = file.Directory.FullName;
                var name = file.Name.Substring(0, file.Name.IndexOf(".tar"));
                keyCrypted = Path.Combine(dir, name + ".key.encrypted");
                key = Path.Combine(dir, name + ".key");

                // crypt passfile
                string publicKeyPath = Path.Combine(Environment.CurrentDirectory, option.PublicPrivateKeyFile);


                var keyDecryptedResult = DecryptRandomKey(key, keyCrypted, option.PublicPrivateKeyFile).Run(block.DestinationFolder, cancellationToken);
                option.PasswordFile = key;
            }
        }

        private static string CompressCommand(Block block, TccOption option, ExternalDependecies ext)
        {
            string cmd;
            switch (option.PasswordMode)
            {
                case PasswordMode.None:
                    // tar -c C:\SourceFolder | lz4.exe -1 - compressed.tar.lz4
                    cmd = $"{ext.Tar().Escape()} -c {block.Source.Escape()}";
                    cmd += $"{Pipe}{ext.Lz4().Escape()} -1 -v - {block.DestinationArchive.Escape()}";
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option);
                    // tar -c C:\SourceFolder | lz4.exe -1 - | openssl aes-256-cbc -k "password" -out crypted.tar.lz4.aes
                    cmd = $"{ext.Tar().Escape()} -c {block.Source.Escape()}";
                    cmd += $"{Pipe}{ext.Lz4().Escape()} -1 -v - ";
                    cmd += $"{Pipe}{ext.OpenSsl().Escape()} aes-256-cbc {passwdCommand} -out {block.DestinationArchive.Escape()}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd;
        }


        private static string DecompressCommand(Block block, TccOption option, ExternalDependecies ext)
        {
            string cmd;
            switch (option.PasswordMode)
            {
                case PasswordMode.None:
                    //lz4 archive.tar.lz4 -dc --no-sparse | tar xf -
                    cmd = $"{ext.Lz4().Escape()} {block.Source.Escape()} -dc --no-sparse ";
                    cmd += $"{Pipe}{ext.Tar().Escape()} xf - ";
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option);
                    //openssl aes-256-cbc -d -k "test" -in crypted.tar.lz4.aes | lz4 -dc --no-sparse - | tar xf -
                    cmd = $"{ext.OpenSsl().Escape()} aes-256-cbc -d {passwdCommand} -in {block.Source}";
                    cmd += $"{Pipe}{ext.Lz4().Escape()} -dc --no-sparse - ";
                    cmd += $"{Pipe}{ext.Tar().Escape()} xf - ";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd;
        }

        private static string PasswordCommand(TccOption option)
        {
            string passwdCommand;
            if (option.PasswordMode == PasswordMode.InlinePassword)
            {
                if (String.IsNullOrWhiteSpace(option.Password))
                {
                    throw new CommandLineException(CommandLineError.PasswordMissing);
                }
                passwdCommand = "-k " + option.Password;
            }
            else
            {
                if (String.IsNullOrWhiteSpace(option.PasswordFile))
                {
                    throw new CommandLineException(CommandLineError.PasswordFileMissing);
                }
                passwdCommand = "-kfile " + option.PasswordFile;
            }

            return passwdCommand;
        }

        private static string EncryptRandomKey(string keyPath, string keyCryptedPath, string publicKey)
        {
            if (String.IsNullOrWhiteSpace(publicKey))
            {
                throw new CommandLineException(CommandLineError.PasswordPublicKeyMissing);
            }
            return $"openssl rsautl -encrypt -inkey {publicKey} -pubin -in {keyPath} -out {keyCryptedPath}";
        }

        private static string DecryptRandomKey(string keyPath, string keyCryptedPath, string privateKey)
        {
            if (String.IsNullOrWhiteSpace(privateKey))
            {
                throw new CommandLineException(CommandLineError.PasswordPrivateKeyMissing);
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

    public class CommandLineException : Exception
    {
        public CommandLineException(CommandLineError error)
        {
            Error = error;
        }

        public CommandLineError Error { get; set; }
    }

    public enum CommandLineError
    {
        PasswordMissing,
        PasswordFileMissing,
        PasswordPublicKeyMissing,
        PasswordPrivateKeyMissing
    }
}