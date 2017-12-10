using System;
using System.Collections.Generic;
using CommandLine;

namespace TCC
{
	class Program
	{
		static void Main(string[] args)
		{
			var parser = new Parser(config =>
			{
				config.EnableDashDash = true;
				config.HelpWriter = Console.Out;
			});

			var returnCode = parser.ParseArguments<CompressOption, DecompressOption>(args)
				.MapResult(
					(CompressOption opts) => TarCompressCrypt.Compress(opts),
					(DecompressOption opts) => TarCompressCrypt.Decompress(opts),
					errs => 1);

			Environment.Exit(returnCode);
		}

	}
}
