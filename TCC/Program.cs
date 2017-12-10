using System;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;

namespace TCC
{
	class Program
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
		private delegate bool ConsoleEventDelegate(int eventType);

		static void Main(string[] args)
		{
			var parser = new Parser(config =>
			{
				config.EnableDashDash = true;
				config.HelpWriter = Console.Out;
			});
			int counter = 0;
			var subject = new Subject<CommandResult>();
			subject.Subscribe(r =>
			{
				Interlocked.Increment(ref counter);
				Console.WriteLine(counter + "/" + r.BatchTotal + " : " + r.Block.ArchiveName);
				if (r.HasError)
				{
					Console.Error.WriteLine("Error : " + r.Errors);
				}
				if (!String.IsNullOrEmpty(r.Output))
				{
					Console.Out.WriteLine("Info : " + r.Errors);
				}
			});

			var cts = new CancellationTokenSource();
			bool ConsoleEventCallback(int eventType)
			{
				Console.Error.WriteLine("!!! Process termination requested");
				cts.Cancel();
				return false;
			}
			SetConsoleCtrlHandler(ConsoleEventCallback, true);

			var returnCode = parser.ParseArguments<CompressOption, DecompressOption>(args)
				.MapResult(
					(CompressOption opts) => TarCompressCrypt.Compress(opts, subject, cts.Token),
					(DecompressOption opts) => TarCompressCrypt.Decompress(opts, subject, cts.Token),
					errs => 1);

			Environment.Exit(returnCode);
		}

	}
}
