using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Command
{
    public static class ProcessStartHelper
    {
        public static Task<CommandResult> Run(this string command, string workingDirectory,
            CancellationToken cancellationToken, TimeSpan? timeout = null)
        {
            return command.Run(workingDirectory, cancellationToken, TccProcessOutputClassifier.Instance, timeout);
        }

        private static Task<CommandResult> Run(this string command, string workingDirectory,
            CancellationToken cancellationToken, IProcessOutputClassifier outputClassifier, TimeSpan? timeout = null)
        {
            TimeSpan timeOutSpan = timeout ?? TimeSpan.FromMinutes(120);

            Process process = CreateProcess(command, workingDirectory);

            var standardOutput = new List<string>();
            var standardError = new List<string>();
            var standardInfos = new List<string>();
            var standardOutputResults = new TaskCompletionSource<List<string>>();
            var standardErrorResults = new TaskCompletionSource<List<string>>();

            process.OutputDataReceived += (sender, e) => OnOutput(e, standardOutput, standardOutputResults);
            process.ErrorDataReceived += (sender, e) => OnError(outputClassifier, e, standardError, standardInfos, standardErrorResults);

            var tcs = new TaskCompletionSource<CommandResult>();

            CancellationTokenRegistration registration = default;

            void OnDispose()
            {
                process.Exited -= OnProcessExit;
                registration.Dispose();
            }

            var stopwatch = new Stopwatch();

            async void OnProcessExit(object sender, EventArgs args)
            {
                stopwatch.Stop();
                var stdout = await standardOutputResults.Task;
                var stderr = await standardErrorResults.Task;

                var result = new CommandResult
                {
                    Command = command,
                    ExitCode = process.ExitCode,
                    IsSuccess = standardError.Count == 0,
                    Output = string.Join(Environment.NewLine, stdout),
                    Errors = string.Join(Environment.NewLine, stderr),
                    Infos = standardInfos,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };

                tcs.TrySetResult(result);
                OnDispose();
            }

            process.Exited += OnProcessExit;

            registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
                KillProcessAndChilds(process);
                OnDispose();
            });
            
            try
            {
                stopwatch.Start();
                if (!process.Start())
                {
                    tcs.TrySetException(new InvalidOperationException("Failed to start process"));
                    OnDispose();
                }
                else
                {
                    Task.Run(() =>
                    {
                        if (!process.WaitForExit((int)Math.Min(timeOutSpan.TotalMilliseconds, int.MaxValue)))
                        {
                            tcs.TrySetException(new InvalidOperationException("Process timedout"));
                            OnDispose();
                        }
                    });
                }
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
                OnDispose();
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return tcs.Task;

        }

        private static void KillProcessAndChilds(Process process)
        {
            try
            {
                KillProcessAndChildren(process.Id);
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already killed, do nothing
            }
        }

        private static void OnError(IProcessOutputClassifier outputClassifier, DataReceivedEventArgs e, List<string> standardError, List<string> standardInfos, TaskCompletionSource<List<string>> standardErrorResults)
        {
            if (e.Data == null)
            {
                standardErrorResults.SetResult(standardError);
            }
            else
            {
                if (outputClassifier?.IsIgnorable(e.Data) == true)
                {
                    return;
                }
                if (outputClassifier?.IsInfo(e.Data) == true)
                {
                    standardInfos.Add(e.Data);
                }
                else
                {
                    standardError.Add(e.Data);
                }
            }
        }

        private static void OnOutput(DataReceivedEventArgs e, List<string> standardOutput, TaskCompletionSource<List<string>> standardOutputResults)
        {
            if (e.Data == null)
            {
                standardOutputResults.SetResult(standardOutput);
            }
            else
            {
                standardOutput.Add(e.Data);
            }
        }   

        private static Process CreateProcess(string command, string workingDirectory)
        {
            var psInfo = new ProcessStartInfo("cmd.exe", " /c \"" + command + "\"")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            return new Process
            {
                StartInfo = psInfo,
                EnableRaisingEvents = true
            };
        }

        private static void KillProcessAndChildren(int pid)
        {
            using (var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
            {
                var moc = searcher.Get();
                foreach (var o in moc)
                {
                    var mo = (ManagementObject)o;
                    KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
                }
                try
                {
                    var proc = Process.GetProcessById(pid);
                    proc.Kill();
                }
                catch (Exception)
                {
                    // Process already exited.
                }
            }
        }
    }
}