using System;
using System.IO;
using System.Linq;
using CommandLine;
using TCC.Lib;
using TCC.Lib.Blocks;
using TCC.Lib.Options;

namespace TCC.Parser
{
    public static class ParseCommandLineHelper
    {
        public static TccCommand ParseCommandLine(this string[] args)
        {
            TccCommand command;

            var parsed = CommandLine.Parser.Default.ParseArguments<CompressCmdOptions, DecompressOptions, BenchmarkOptions>(args);
            try
            {
                command = parsed.MapResult(
                    (CompressCmdOptions opts) =>
                    {
                        var option = new CompressOption
                        {
                            Algo = opts.Algorithm,
                            CompressionRatio = opts.Ratio,
                            DestinationDir = opts.Output,
                            FailFast = opts.FailFast,
                            Verbose = opts.Verbose,
                            SourceDirOrFile = opts.Source.FirstOrDefault(),
                            BlockMode = opts.Individual ? BlockMode.Individual : BlockMode.Explicit,
                            Threads = ExtractThreads(opts),
                            BackupMode = opts.BackupMode,
                            Retry = opts.Retry,
                            Filter = opts.Filter,
                            SlackChannel = opts.SlackChannel,
                            SlackSecret = opts.SlackSecret,
                            BucketName = opts.BucketName,
                            SlackOnlyOnError = opts.SlackOnlyOnError
                        };

                        ExtractPasswordInfo(opts, option, Mode.Compress);

                        return new TccCommand
                        {
                            Mode = Mode.Compress,
                            Option = option
                        };
                    },
                    (DecompressOptions opts) =>
                    {
                        var option = new DecompressOption
                        {
                            DestinationDir = opts.Output,
                            FailFast = opts.FailFast,
                            Verbose = opts.Verbose,
                            SourceDirOrFile = opts.Source.FirstOrDefault(),
                            Threads = ExtractThreads(opts),
                            SlackChannel = opts.SlackChannel,
                            SlackSecret = opts.SlackSecret,
                            BucketName = opts.BucketName,
                            SlackOnlyOnError = opts.SlackOnlyOnError
                        };

                        ExtractPasswordInfo(opts, option, Mode.Decompress);

                        return new TccCommand
                        {
                            Mode = Mode.Decompress,
                            Option = option
                        };
                    },
                    (BenchmarkOptions opts) =>
                    {
                        bool IsExplicitMode()
                        {
                            return !args.Any(i => BenchmarkOptions.AutoTestDataOptions.Contains(i));
                        }

                        var option = new BenchmarkOption
                        {
                            Algorithm = opts.Algorithm,
                            Ratios = opts.Ratios,
                            Encrypt = opts.Encrypt,
                            Source = IsExplicitMode() ? opts.Source : null,
                            Content = opts.Content,
                            NumberOfFiles = opts.NumberOfFiles,
                            FileSize = opts.FileSize,
                            Threads = opts.Threads,
                            OutputCompressed = opts.OutputCompressed,
                            OutputDecompressed = opts.OutputDecompressed,
                            Cleanup = opts.Cleanup
                        };
                        return new TccCommand
                        {
                            Mode = Mode.Benchmark,
                            BenchmarkOption = option
                        };
                    },
                    errs => new TccCommand { ReturnCode = 1 });
            }
            catch (CommandLineException ae)
            {
                Console.Out.WriteLine(ae.Message);
                return new TccCommand { ReturnCode = 1 };
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.ToString());
                return new TccCommand { ReturnCode = 1 };
            }
            return command;
        }

        private static int ExtractThreads(BaseCmdOptions opts)
        {
            if (string.IsNullOrEmpty(opts.Threads))
            {
                return 1;
            }
            if (string.Equals(opts.Threads, "all", StringComparison.InvariantCultureIgnoreCase))
            {
                return Environment.ProcessorCount;
            }
            if (int.TryParse(opts.Threads, out var nbThread))
            {
                return nbThread;
            }
            throw new CommandLineException("Maximum threads need to be either numeric, or \"all\" ");
        }

        private static void ExtractPasswordInfo(BaseCmdOptions opts, TccOption option, Mode mode)
        {
            if (!String.IsNullOrEmpty(opts.Password))
            {
                ExtractInlinePassword(opts, option);
            }
            if (!String.IsNullOrEmpty(opts.PasswordFile))
            {
                ExtractPasswordFile(opts, option);
            }
            if (!String.IsNullOrEmpty(opts.PasswordKey))
            {
                ExtractAsymetricFile(opts, option, mode);
            }
        }

        private static void ExtractAsymetricFile(BaseCmdOptions opts, TccOption option, Mode mode)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(opts.PasswordKey))
                throw new CommandLineException("Public or private key must be specified");

            if (!File.Exists(opts.PasswordKey))
                throw new CommandLineException("Public or private key file doesn't exists");

            switch (mode)
            {
                case Mode.Compress:
                    option.PasswordOption = new PublicKeyPasswordOption
                    {
                        PublicKeyFile = opts.PasswordKey
                    };
                    break;
                case Mode.Decompress:
                    option.PasswordOption = new PrivateKeyPasswordOption
                    {
                        PrivateKeyFile = opts.PasswordKey
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static void ExtractPasswordFile(BaseCmdOptions opts, TccOption option)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(opts.PasswordFile))
                throw new CommandLineException("Password file must be specified");

            if (!File.Exists(opts.PasswordFile))
                throw new CommandLineException("Password file doesn't exists");

            option.PasswordOption = new PasswordFileOption
            {
                PasswordFile = opts.PasswordFile
            };
        }

        private static void ExtractInlinePassword(BaseCmdOptions opts, TccOption option)
        {
            if (option.PasswordOption != NoPasswordOption.Nop)
                throw new CommandLineException("Only one password mode allowed");

            if (string.IsNullOrEmpty(opts.Password))
                throw new CommandLineException("Password must be specified");

            option.PasswordOption = new InlinePasswordOption
            {
                Password = opts.Password
            };
        }
    }
}