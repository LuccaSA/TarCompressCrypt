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
            Func<Block, string, CancellationToken, CommandResult> processor,
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
                var result = processor(b, option.Password, cancellationToken);
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
                        Destination = dstDir.FullName,
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
                    Destination = dstDir.FullName,
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
                    if (dstDir.Exists == false)
                    {
                        dstDir.Create();
                    }
                }

                // for each directory in sourceDir we create an archive
                foreach (DirectoryInfo di in directories)
                {
                    string name = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(di.Name) + extension);
                    blocks.Add(new Block
                    {
                        OperationFolder = srcDir.FullName,
                        Source = di.Name,
                        Destination = name,
                        ArchiveName = name
                    });
                }

                // for each file in sourceDir we create an archive
                foreach (FileInfo fi in files)
                {
                    string name = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension);
                    blocks.Add(new Block
                    {
                        OperationFolder = srcDir.FullName,
                        Source = fi.Name,
                        Destination = name,
                        ArchiveName = name
                    });
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            return blocks;
        }

        private static CommandResult Encrypt(Block block, string password, CancellationToken cancellationToken)
        {
            var ext = new ExternalDependecies();
            // openssl aes-256-cbc -d -k "test" -in crypt5.lz4 | lz4 -dc --no-sparse - | tar xf -
            var cmd = ext.Tar().Escape() + " -c " + block.Source.Escape();
            cmd += Pipe + ext.Lz4().Escape() + " -1 - ";
            cmd += Pipe + ext.OpenSsl().Escape() + " aes-256-cbc -k " + password + " -out " + block.Destination.Escape();

            var result = cmd.Run(block.OperationFolder, cancellationToken);
            return result;
        }

        private static CommandResult Decrypt(Block block, string password, CancellationToken cancellationToken)// string password, string outDirectory)
        {
            var ext = new ExternalDependecies();
            // openssl aes-256-cbc -d -k "test" -in crypt3.lz4 | lz4 -dc --no-sparse - | tar xf -
            var cmd = ext.OpenSsl().Escape() + " aes-256-cbc -d -k " + password + " -in " + block.Source;
            cmd += Pipe + ext.Lz4().Escape() + " -dc --no-sparse - ";
            cmd += Pipe + ext.Tar().Escape() + " xf - ";
            var result = cmd.Run(block.OperationFolder, cancellationToken);

            return result;
        }
    }
}