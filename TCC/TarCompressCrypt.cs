using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace TCC
{
	public class TarCompressCrypt
	{
		private const string ExeTar = @"C:\Program Files\Git\usr\bin\tar.exe";
		private const string ExeLz4 = @"C:\Program Files (x86)\lz4_v1_8_0_win64\lz4.exe";
		private const string OpenSsl = @"C:\Program Files\Git\usr\bin\openssl.exe";
		private const string Pipe = @" | ";

		public static int Compress(CompressOption compressOption, Subject<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			List<Block> blocks = PreprareCompressBlocks(compressOption.SourceDirOrFile, compressOption.DestinationDir, compressOption.Individual, !string.IsNullOrEmpty(compressOption.Password));

			ParallelOptions po = ParallelOptions(compressOption.Threads);

			var pr = Parallel.ForEach(blocks, po, (b, state) =>
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				var result = Encrypt(b, compressOption.Password, cancellationToken);
				result.Block = b;
				obervableLog?.OnNext(result);
				if (result.HasError && compressOption.FailFast)
				{
					state.Break();
				}
			});

			return pr.IsCompleted ? 0 : 1;
		}

		public static int Decompress(DecompressOption decompressOption, Subject<CommandResult> obervableLog = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			List<Block> blocks = PreprareDecompressBlocks(decompressOption.SourceDirOrFile, decompressOption.DestinationDir, !string.IsNullOrEmpty(decompressOption.Password));

			ParallelOptions po = ParallelOptions(decompressOption.Threads);

			var pr = Parallel.ForEach(blocks, po, (b, state) =>
			{
				if (cancellationToken.IsCancellationRequested)
				{
					return;
				}
				var result = Decrypt(b, decompressOption.Password, cancellationToken);
				result.Block = b;
				obervableLog?.OnNext(result);
				if (result.HasError && decompressOption.FailFast)
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
						Destination = dstDir.FullName
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
					Destination = dstDir.FullName
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
					blocks.Add(new Block
					{
						OperationFolder = srcDir.FullName,
						Source = di.Name,
						Destination = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(di.Name) + extension)
					});
				}

				// for each file in sourceDir we create an archive
				foreach (FileInfo fi in files)
				{
					blocks.Add(new Block
					{
						OperationFolder = srcDir.FullName,
						Source = fi.Name,
						Destination = Path.Combine(dstDir.FullName, Path.GetFileNameWithoutExtension(fi.Name) + extension)
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
			// openssl aes-256-cbc -d -k "test" -in crypt5.lz4 | lz4 -dc --no-sparse - | tar xf -
			var cmd = ExeTar.Escape() + " -c " + block.Source.Escape();
			cmd += Pipe + ExeLz4.Escape() + " -1 - ";
			cmd += Pipe + OpenSsl.Escape() + " aes-256-cbc -k " + password + " -out " + block.Destination.Escape();

			var result = cmd.Run(block.OperationFolder, cancellationToken);
			return result;
		}

		private static CommandResult Decrypt(Block block, string password, CancellationToken cancellationToken)// string password, string outDirectory)
		{
			// openssl aes-256-cbc -d -k "test" -in crypt3.lz4 | lz4 -dc --no-sparse - | tar xf -
			var cmd = OpenSsl.Escape() + " aes-256-cbc -d -k " + password + " -in " + block.Source;
			cmd += Pipe + ExeLz4.Escape() + " -dc --no-sparse - ";
			cmd += Pipe + ExeTar.Escape() + " xf - ";
			var result = cmd.Run(block.OperationFolder, cancellationToken);

			return result;
		}
	}
}