using System;
using System.Threading.Tasks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using TCC.Parser;
using Xunit;

namespace TCC.Tests.Parsing
{
    public class ParsingTests
    {
        [Theory]
        [InlineData("compress")]
        [InlineData("decompress")]
        [InlineData("file.txt")]
        [InlineData("decompress file.txt -o temp -i true")]
        [InlineData("compress file.txt -o temp -t true")]
        public void InvalidCommands(string command)
        {
            var commandBlocks = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            var parsed = commandBlocks.ParseCommandLine();

            Assert.Equal(1, parsed.ReturnCode);
        }

        [Theory]
        [InlineData("compress file.txt -o temp")]
        [InlineData("compress file.txt -o temp -f true")]
        [InlineData("compress file.txt -o temp -f false")]
        [InlineData("decompress file.txt -o temp -f true")]
        [InlineData("decompress file.txt -o temp -f false")]
        [InlineData("compress file.txt -o temp -i true")]
        [InlineData("compress file.txt -o temp -t all")]
        [InlineData("compress file.txt -o temp -t 42")]
        [InlineData("decompress file.txt -o temp")]
        public void ValidCommands(string command)
        {
            var commandBlocks = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            var parsed = commandBlocks.ParseCommandLine();

            Assert.Equal(0, parsed.ReturnCode);
        }

        [Theory]
        [InlineData("compress file.txt -r 3 -a Lz4 -o temp", CompressionAlgo.Lz4)] 
        [InlineData("compress file.txt -r 3 -a Brotli -o temp", CompressionAlgo.Brotli)] 
        [InlineData("compress file.txt -r 3 -a Zstd -o temp", CompressionAlgo.Zstd)]
        public void AlgoCommands(string command, CompressionAlgo algo)
        {
            var commandBlocks = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            var parsed = commandBlocks.ParseCommandLine();
            Assert.Equal(0, parsed.ReturnCode);
            Assert.IsType<CompressOption>(parsed.Option);
            Assert.Equal(algo, ((CompressOption)parsed.Option).Algo);
            Assert.Equal(3, ((CompressOption)parsed.Option).CompressionRatio);
        }
         
        [Theory]
        [InlineData("compress file.txt -o temp", null, PasswordMode.None)]
        [InlineData("decompress file.txt -o temp", null, PasswordMode.None)]
        [InlineData("compress file.txt -p 1234 -o temp", "1234", PasswordMode.InlinePassword)]
        [InlineData("decompress file.txt -p 1234 -o temp", "1234", PasswordMode.InlinePassword)]
        [InlineData("compress file.txt -e pass1.txt -o temp", "pass1.txt", PasswordMode.PasswordFile)]
        [InlineData("decompress file.txt -e pass2.txt -o temp", "pass2.txt", PasswordMode.PasswordFile)]
        [InlineData("compress file.txt -k pass3.pem -o temp", "pass3.pem", PasswordMode.PublicKey)]
        [InlineData("decompress file.txt -k pass4.pem -o temp", "pass4.pem", PasswordMode.PublicKey)]
        public async Task EcryptionCommands(string command, string passchain, PasswordMode mode)
        {
            var commandBlocks = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (passchain != null && passchain.Contains("."))
            {
                passchain.CreateEmptyFile(); // we create fake pass files
            }

            var parsed = commandBlocks.ParseCommandLine();

            Assert.Equal(mode, parsed.Option.PasswordOption.PasswordMode);

            string passfound = null;

            switch (parsed.Option.PasswordOption)
            {
                case InlinePasswordOption po:
                    passfound = po.Password;
                    break;
                case PasswordFileOption pf:
                    passfound = pf.PasswordFile;
                    break;
                case PublicKeyPasswordOption puk:
                    passfound = puk.PublicKeyFile;
                    break;
                case PrivateKeyPasswordOption pik:
                    passfound = pik.PrivateKeyFile;
                    break;
                case NoPasswordOption _:
                    break;
                default:
                    throw new NotImplementedException();
            }
            Assert.Equal(passchain, passfound);
            Assert.Equal(0, parsed.ReturnCode);

            if (passchain != null && passchain.Contains("."))
            {
                await passchain.TryDeleteFileWithRetryAsync();
            }
        }
    }
}
