using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace TCC
{
    public static class TarCompressCrypt
    {
        private const string Pipe = @" | ";

        public static int Compress(CompressOption compressOption, Subject<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Block> blocks = PreprareCompressBlocks(compressOption.SourceDirOrFile, compressOption.DestinationDir, compressOption.Individual, !string.IsNullOrEmpty(compressOption.Password));

            return ProcessingLoop(blocks, compressOption, Encrypt, obervableLog, cancellationToken);
        }

        public static int Decompress(DecompressOption decompressOption, Subject<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            List<Block> blocks = PreprareDecompressBlocks(decompressOption.SourceDirOrFile, decompressOption.DestinationDir, !string.IsNullOrEmpty(decompressOption.Password));

            return ProcessingLoop(blocks, decompressOption, Decrypt, obervableLog, cancellationToken);
        }

        private static int ProcessingLoop(List<Block> blocks,
            TccOption option,
            Func<Block, TccOption, CancellationToken, CommandResult> processor,
            Subject<CommandResult> obervableLog = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ParallelOptions po = ParallelOptions(option.Threads);

            var pr = Parallel.ForEach(blocks, po, (b, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                var result = processor(b, option, cancellationToken);
                result.Block = b;
                result.BatchTotal = blocks.Count;
                obervableLog?.OnNext(result);
                if (result.HasError && option.FailFast)
                {
                    state.Break();
                }
            });

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

        private static List<Block> PreprareDecompressBlocks(string sourceDir, string destinationDir, bool crypt)
        {
            var blocks = new List<Block>();
            string extension = crypt ? ".tar.lz4.aes" : ".tar.lz4";
            if (Directory.Exists(sourceDir))
            {
                var srcDir = new DirectoryInfo(sourceDir);
                var found = srcDir.EnumerateFiles("*" + extension).ToList();
                var dstDir = new DirectoryInfo(destinationDir);
                if (found.Count != 0)
                {
                    if (dstDir.Exists == false)
                    {
                        dstDir.Create();
                    }
                }

                foreach (FileInfo fi in found)
                {
                    blocks.Add(new Block
                    {
                        OperationFolder = dstDir.FullName,
                        Source = fi.FullName,
                        DestinationFolder = dstDir.FullName,
                        ArchiveName = fi.FullName
                    });
                }
            }
            else if (File.Exists(sourceDir) && Path.HasExtension(extension))
            {
                var dstDir = new DirectoryInfo(destinationDir);
                if (dstDir.Exists == false)
                {
                    dstDir.Create();
                }
                blocks.Add(new Block
                {
                    OperationFolder = dstDir.FullName,
                    Source = sourceDir,
                    DestinationFolder = dstDir.FullName,
                    ArchiveName = new FileInfo(sourceDir).FullName
                });
            }
            return blocks;
        }

        private static List<Block> PreprareCompressBlocks(string sourceDir, string destinationDir, bool individual, bool crypt)
        {
            var blocks = new List<Block>();

            string extension = crypt ? ".tar.lz4.aes" : ".tar.lz4";

            if (individual)
            {
                var srcDir = new DirectoryInfo(sourceDir);
                var dstDir = new DirectoryInfo(destinationDir);

                List<FileInfo> files = srcDir.EnumerateFiles().ToList();
                List<DirectoryInfo> directories = srcDir.EnumerateDirectories().ToList();

                if (files.Count != 0 || directories.Count != 0)
                {
                    if (!dstDir.Exists)
                    {
                        dstDir.Create();
                    }
                }

                // for each directory in sourceDir we create an archive
                foreach (DirectoryInfo di in directories)
                {
                    blocks.Add(new Block
                    {
                        OperationFolder = srcDir.FullName,
                        Source = di.Name,
                        DestinationArchive = Path.Combine(dstDir.FullName, di.Name + extension),
                        DestinationFolder = dstDir.FullName,
                        ArchiveName = di.Name
                    });
                }

                // for each file in sourceDir we create an archive
                foreach (FileInfo fi in files)
                {
                    blocks.Add(new Block
                    {
                        OperationFolder = srcDir.FullName,
                        Source = fi.Name,
                        DestinationArchive = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension),
                        DestinationFolder = dstDir.FullName,
                        ArchiveName = Path.GetFileNameWithoutExtension(fi.Name)
                    });
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            return blocks;
        }

        private static CommandResult Encrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();
            string key, keyCrypted;

            PrepareEncryptionKey(block, option, out key, out keyCrypted, cancellationToken);

            string cmd = CompressCommand(block, option, ext);
            var result = cmd.Run(block.OperationFolder, cancellationToken);

            CleanupKey(block, option, key, keyCrypted, result, Mode.Compress);

            return result;
        }

        private static CommandResult Decrypt(Block block, TccOption option, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();
            string key, keyCrypted;

            PrepareDecryptionKey(block, option, out key, out keyCrypted, cancellationToken);

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
                string publicKeyPath = Path.Combine(Environment.CurrentDirectory, option.PublicPrivateKeyFile);
                var keyCryptedResult = EncryptRandomKey(key, keyCrypted, publicKeyPath).Run(block.DestinationFolder, cancellationToken);
                option.PasswordFile = key;
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
                // crypt passfile
                string publicKeyPath = Path.Combine(Environment.CurrentDirectory, option.PublicPrivateKeyFile);
                var keyDecryptedResult = DecryptRandomKey(key, keyCrypted, publicKeyPath).Run(block.DestinationFolder, cancellationToken);
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
                    string passwdCommand;
                    if (option.PasswordMode == PasswordMode.InlinePassword)
                    {
                        passwdCommand = "-k " + option.Password;
                    }
                    else
                    {
                        passwdCommand = "-kfile " + option.PasswordFile;
                    }
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
                    //lz4 archive.tar.lz4 - dc--no - sparse - | tar xf -
                    cmd = $"{Pipe}{ext.Lz4().Escape()} -dc --no-sparse - ";
                    cmd += $"{Pipe}{ext.Tar().Escape()} xf - ";
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:

                    string passwdCommand;
                    if (option.PasswordMode == PasswordMode.InlinePassword)
                    {
                        passwdCommand = "-k " + option.Password;
                    }
                    else
                    {
                        passwdCommand = "-kfile " + option.PasswordFile;
                    }
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

        private static string EncryptRandomKey(string keyPath, string keyCryptedPath, string publicKey)
        {
            return $"openssl rsautl -encrypt -inkey {publicKey} -pubin -in {keyPath} -out {keyCryptedPath}";
        }

        private static string DecryptRandomKey(string keyPath, string keyCryptedPath, string privateKey)
        {
            return $"openssl rsautl -decrypt -inkey {privateKey} -pubin -in {keyCryptedPath} -out {keyPath}";
        }

        private static string GenerateRandomKey(string filename)
        {
            // 512 byte == 4096 bit
            return $"openssl rand -base64 512 > {filename}";
        }

    }
}