using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace TCC
{
	public static class CommandHelper
	{

		public static CommandResult Run(this string command, string workingDirectory, int timeoutMinutes = 30)
		{
			var timeoutLimit = TimeSpan.FromMinutes(timeoutMinutes);

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = " /c \"" + command + "\"",
					WorkingDirectory = workingDirectory,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				}
			};

			var output = new StringBuilder();
			var error = new StringBuilder();

			using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
			{
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
				{
					process.OutputDataReceived += (sender, e) =>
					{
						if (e.Data == null)
						{
							outputWaitHandle.Set();
						}
						else
						{
							output.AppendLine(e.Data);
						}
					};

					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data == null)
						{
							errorWaitHandle.Set();
						}
						else
						{
							if (e.Data == @"/usr/bin/tar: Removing leading `C:\' from member names")
							{
								;
							}
							else
							{
								error.AppendLine(e.Data);
							}
						}
					};

					process.Start();

					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					int timeout = (int)timeoutLimit.TotalMilliseconds;

					if (process.WaitForExit(timeout) &&
					    outputWaitHandle.WaitOne(timeout) &&
					    errorWaitHandle.WaitOne(timeout))
					{
						return new CommandResult
						{
							ExitCode = process.ExitCode,
							IsSuccess = true,
							Output = output.ToString(),
							Errors = error.ToString(),
							Command = command
						};
					}
					else
					{
						return new CommandResult
						{
							ExitCode = process.ExitCode,
							IsSuccess = false,
							Output = output.ToString(),
							Errors = error.ToString(),
							Command = command
						};
					}
				}
			}
		}

	}
}