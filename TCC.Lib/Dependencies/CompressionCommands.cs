using System;
using System.Text;
using TCC.Lib.Blocks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Dependencies
{
    public class CompressionCommands
    {
        private readonly ExternalDependencies _ext;
        public CompressionCommands(ExternalDependencies externalDependencies)
        {
            _ext = externalDependencies;
        }

        public string CompressCommand(CompressionBlock block, CompressOption option)
        {
            var cmd = new StringBuilder();
            string ratio;

            switch (option.Algo)
            {
                case CompressionAlgo.Lz4:
                    ratio = option.CompressionRatio != 0 ? $"-{option.CompressionRatio}" : string.Empty;
                    break;
                case CompressionAlgo.Brotli:
                    ratio = option.CompressionRatio != 0 ? $"-q {option.CompressionRatio}" : string.Empty;
                    break;
                case CompressionAlgo.Zstd:
                    ratio = option.CompressionRatio != 0 ? $"-{option.CompressionRatio}" : string.Empty;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
            }

            cmd.Append($"{_ext.Tar()} -c {block.Source}");
            if (block.BackupMode == Database.BackupMode.Diff)
            {
                if (!block.DiffDate.HasValue)
                {
                    throw new Exception("todo ex msg");
                }
                cmd.Append($" --newer-mtime \"{block.DiffDate.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}\"");
            }

            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    // tar -c C:\SourceFolder | lz4.exe -1 - compressed.tar.lz4
                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {_ext.Lz4()} {ratio} -v - {block.DestinationArchive}");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {_ext.Brotli()} {ratio} - -o {block.DestinationArchive}");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {_ext.Zstd()} --no-progress {ratio} - -o {block.DestinationArchive}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
                    }
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option, block);
                    // tar -c C:\SourceFolder | lz4.exe -1 - | openssl aes-256-cbc -k "password" -out crypted.tar.lz4.aes
                    switch (option.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {_ext.Lz4()} {ratio} -v - ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {_ext.Brotli()} {ratio} - ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {_ext.Zstd()} --no-progress {ratio} - ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(option), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {_ext.OpenSsl()} aes-256-cbc {passwdCommand} -out {block.DestinationArchive}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd.ToString();
        }

        public string DecompressCommand(DecompressionBlock block, TccOption option)
        {
            var cmd = new StringBuilder();
            switch (option.PasswordOption.PasswordMode)
            {
                case PasswordMode.None:
                    //lz4 archive.tar.lz4 -dc --no-sparse | tar xf -
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($"{_ext.Lz4()} {block.Source} -dc --no-sparse ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($"{_ext.Brotli()} {block.Source} -d -c ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($"{_ext.Zstd()} --no-progress {block.Source} -d -c ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {_ext.Tar()} xf - ");
                    break;
                case PasswordMode.InlinePassword:
                case PasswordMode.PasswordFile:
                case PasswordMode.PublicKey:
                    string passwdCommand = PasswordCommand(option, block);
                    //openssl aes-256-cbc -d -k "test" -in crypted.tar.lz4.aes | lz4 -dc --no-sparse - | tar xf -
                    cmd.Append($"{_ext.OpenSsl()} aes-256-cbc -d {passwdCommand} -in {block.Source}");
                    switch (block.Algo)
                    {
                        case CompressionAlgo.Lz4:
                            cmd.Append($" | {_ext.Lz4()} -dc --no-sparse - ");
                            break;
                        case CompressionAlgo.Brotli:
                            cmd.Append($" | {_ext.Brotli()} - -d ");
                            break;
                        case CompressionAlgo.Zstd:
                            cmd.Append($" | {_ext.Zstd()} --no-progress - -d ");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(block), "Unknown PasswordMode");
                    }
                    cmd.Append($" | {_ext.Tar()} xf - ");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(option));
            }
            return cmd.ToString();
        }

        private static string PasswordCommand(TccOption option, Block block)
        {
            string passwdCommand;
            if (option.PasswordOption.PasswordMode == PasswordMode.InlinePassword
                && option.PasswordOption is InlinePasswordOption inlinePassword)
            {
                if (String.IsNullOrWhiteSpace(inlinePassword.Password))
                {
                    throw new CommandLineException("Password missing");
                }
                passwdCommand = "-k " + inlinePassword.Password;
            }
            else
            {
                if (String.IsNullOrWhiteSpace(block.BlockPasswordFile))
                {
                    throw new CommandLineException("Password file missing");
                }
                passwdCommand = "-kfile " + block.BlockPasswordFile.Escape();
            }

            return passwdCommand;
        }
    }
}
