using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System;
using System.CommandLine.Hosting;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using TCC.Lib.Blocks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests.Parsing
{
    public class ParsingTests
    {
        [Theory]
        [InlineData("compress")]
        [InlineData("decompress")]
        [InlineData("file.txt")]
        public async Task InvalidCommandsAsync(string command)
        {
            var commandLine = Program.BuildCommandLine().Build();

            var testConsole = new TestConsole();
            var returnCode = await commandLine.InvokeAsync(command, testConsole);

            returnCode.Should().Be(1);
            testConsole.Out.ToString().Should().Contain("Usage:");
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
        public async Task ValidCommandsAsync(string command)
        {
            var controllerMock = new Mock<ITccController>();
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync(command);

            returnCode.Should().Be(0);
        }

        [Theory]
        [InlineData("compress file.txt -r 3 -a Lz4 -o temp", CompressionAlgo.Lz4)]
        [InlineData("compress file.txt -r 3 -a Brotli -o temp", CompressionAlgo.Brotli)]
        [InlineData("compress file.txt -r 3 -a Zstd -o temp", CompressionAlgo.Zstd)]
        public async Task AlgoCommandsAsync(string command, CompressionAlgo algo)
        {
            CompressOption capturedCompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync(command);

            returnCode.Should().Be(0);
            capturedCompressOption.Should().NotBeNull();
            capturedCompressOption.Algo.Should().Be(algo);
            capturedCompressOption.CompressionRatio.Should().Be(3);
        }

        [Theory]
        [InlineData("compress file.txt -o temp -i", BlockMode.Individual)]
        [InlineData("compress file.txt -o temp -i true", BlockMode.Individual)]
        [InlineData("compress file.txt -o temp -i false", BlockMode.Explicit)]
        [InlineData("compress file.txt -o temp", BlockMode.Explicit)]
        public async Task BlockModeAsync(string command, BlockMode blockMode)
        {
            CompressOption capturedCompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync(command);

            returnCode.Should().Be(0);
            capturedCompressOption.BlockMode.Should().Be(blockMode);
            capturedCompressOption.SourceDirOrFile.Should().Be("file.txt");
        }

        [Theory]
        [InlineData("compress file.txt -o temp", 1)]
        [InlineData("compress file.txt -o temp -t 10", 10)]
        [InlineData("compress file.txt -o temp -t all", null)]
        public async Task ThreadsAsync(string command, int? expectedThreads)
        {
            if (expectedThreads is null)
            {
                expectedThreads = Environment.ProcessorCount;
            }
            CompressOption capturedCompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync(command);

            returnCode.Should().Be(0);
            capturedCompressOption.Threads.Should().Be(expectedThreads);
        }

        [Fact]
        public async Task CompressSourceBindingAsync()
        {
            CompressOption capturedCompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync("compress file.txt -o temp");

            returnCode.Should().Be(0);
            capturedCompressOption.SourceDirOrFile.Should().Be("file.txt");
            capturedCompressOption.DestinationDir.Should().Be("temp");
        }

        [Fact]
        public async Task SourceBindingAsync()
        {
            CompressOption capturedCompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync("compress file.txt -o temp");

            returnCode.Should().Be(0);
            capturedCompressOption.SourceDirOrFile.Should().Be("file.txt");
        }

        [Theory]
        [InlineData("compress file.txt -o temp", null, PasswordMode.None, true)]
        [InlineData("decompress file.txt -o temp", null, PasswordMode.None, false)]
        [InlineData("compress file.txt -p 1234 -o temp", "1234", PasswordMode.InlinePassword, true)]
        [InlineData("decompress file.txt -p 1234 -o temp", "1234", PasswordMode.InlinePassword, false)]
        [InlineData("compress file.txt -e pass1.txt -o temp", "pass1.txt", PasswordMode.PasswordFile, true)]
        [InlineData("decompress file.txt -e pass2.txt -o temp", "pass2.txt", PasswordMode.PasswordFile, false)]
        [InlineData("compress file.txt -k pass3.pem -o temp", "pass3.pem", PasswordMode.PublicKey, true)]
        [InlineData("decompress file.txt -k pass4.pem -o temp", "pass4.pem", PasswordMode.PublicKey, false)]
        public async Task EcryptionCommands(string command, string passchain, PasswordMode mode, bool isCompress)
        {
            if (passchain != null && passchain.Contains("."))
            {
                passchain.CreateEmptyFile(); // we create fake pass files
            }

            CompressOption capturedCompressOption = null;
            DecompressOption capturedDecompressOption = null;
            var controllerMock = new Mock<ITccController>(MockBehavior.Strict);
            controllerMock
                .Setup(c => c.CompressAsync(It.IsAny<CompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<CompressOption>(option => capturedCompressOption = option);
            controllerMock
                .Setup(c => c.DecompressAsync(It.IsAny<DecompressOption>()))
                .Returns(Task.CompletedTask)
                .Callback<DecompressOption>(option => capturedDecompressOption = option);
            var commandLine = Program.BuildCommandLine()
                .UseHost(_ => Host.CreateDefaultBuilder(),
                    host => host.ConfigureServices(services => services.AddSingleton(controllerMock.Object))
                )
                .Build();

            var returnCode = await commandLine.InvokeAsync(command);

            returnCode.Should().Be(0);


            if (isCompress)
            {
                capturedCompressOption.Should().NotBeNull();
                capturedDecompressOption.Should().BeNull();
            }
            else
            {
                capturedCompressOption.Should().BeNull();
                capturedDecompressOption.Should().NotBeNull();
            }

            var passwordOption = capturedCompressOption?.PasswordOption ?? capturedDecompressOption?.PasswordOption;
            passwordOption.Should().NotBeNull();

            passwordOption.PasswordMode.Should().Be(mode);

            string passfound = passwordOption switch
            {
                InlinePasswordOption po => po.Password,
                PasswordFileOption pf => pf.PasswordFile,
                PublicKeyPasswordOption puk => puk.PublicKeyFile,
                PrivateKeyPasswordOption pik => pik.PrivateKeyFile,
                NoPasswordOption _ => null,
                _ => throw new NotImplementedException()
            };

            passfound.Should().Be(passchain);

            if (passchain != null && passchain.Contains("."))
            {
                await passchain.TryDeleteFileWithRetryAsync();
            }
        }
    }
}
