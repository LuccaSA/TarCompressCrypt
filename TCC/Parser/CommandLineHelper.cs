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
        public static (Mode Mode, TccOption Option, int ReturnCode) ParseCommandLine(this string[] args)
        {
            TccOption option = null;
            Mode mode = Mode.Unknown;
            int returnCode = 0;

            var parsed = CommandLine.Parser.Default.ParseArguments<CompressCmdOptions, DecompressOptions, BenchmarkOptions>(args);
            try
            {
                returnCode = parsed.MapResult(
                    (CompressCmdOptions opts) =>
                    {
                        mode = Mode.Compress;
                        option = new CompressOption
                        {
                            Algo = opts.Algorithm,
                            DestinationDir = opts.Output,
                            FailFast = opts.FailFast,
                            SourceDirOrFile = opts.Source.FirstOrDefault(),
                            BlockMode = opts.Individual ? BlockMode.Individual : BlockMode.Explicit, 
                            Threads = ExtractThreads(opts)
                        };

                        ExtractPasswordInfo(opts, option, mode);

                        return 0;
                    },
                    (DecompressOptions opts) =>
                    {
                        mode = Mode.Decompress;
                        option = new DecompressOption
                        {
                            Algo = CompressionAlgo.Lz4,
                            DestinationDir = opts.Output,
                            FailFast = opts.FailFast,
                            SourceDirOrFile = opts.Source.FirstOrDefault(),
                            Threads = ExtractThreads(opts)
                        };

                        ExtractPasswordInfo(opts, option, mode);

                        return 0;
                    },
                    (BenchmarkOptions opts) =>
                    {
                        mode = Mode.Benchmark;
                        option = new DecompressOption();
                        return 0;
                    },
                    errs => 1);
            }
            catch (CommandLineException ae)
            {
                Console.Out.WriteLine(ae.Message);
                returnCode = 1;
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e.ToString());
                returnCode = 1;
            }
            return (mode, option, returnCode);
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
                    option.PasswordOption = new PublicKeyPasswordOption()
                    {
                        PublicKeyFile = opts.PasswordKey
                    };
                    break;
                case Mode.Decompress:
                    option.PasswordOption = new PrivateKeyPasswordOption()
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

            option.PasswordOption = new PasswordFileOption()
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

            option.PasswordOption = new InlinePasswordOption()
            {
                Password = opts.Password
            };
        }
    }
}