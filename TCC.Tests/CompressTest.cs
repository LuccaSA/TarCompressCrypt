﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TCC.Lib;
using TCC.Lib.Benchmark;
using TCC.Lib.Database;
using TCC.Lib.Dependencies;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests
{
    public class CompressTest
    {
        public CompressTest()
        {
            var services = new ServiceCollection();
            services.AddTcc();
            services.AddLogging();
            services.PostConfigure<TccSettings>(i =>
            {
                i.Provider = Provider.SqLite;
            });
            var builder = services.BuildServiceProvider();

            var provider = builder.CreateScope().ServiceProvider;

            _tarCompressCrypt = provider.GetRequiredService<TarCompressCrypt>();
            _externalDependencies = provider.GetRequiredService<ExternalDependencies>();
            _benchmarkOptionHelper = provider.GetRequiredService<BenchmarkOptionHelper>();
            _dbSetup = provider.GetRequiredService<DatabaseSetup>();
        }

        private readonly TarCompressCrypt _tarCompressCrypt;
        private readonly ExternalDependencies _externalDependencies;
        private readonly BenchmarkOptionHelper _benchmarkOptionHelper;
        private readonly DatabaseSetup _dbSetup;

        [Theory]
        [InlineData(PasswordMode.None, CompressionAlgo.Lz4)]
        [InlineData(PasswordMode.InlinePassword, CompressionAlgo.Lz4)]
        [InlineData(PasswordMode.PasswordFile, CompressionAlgo.Lz4)]
        [InlineData(PasswordMode.PublicKey, CompressionAlgo.Lz4)]
        [InlineData(PasswordMode.None, CompressionAlgo.Brotli)]
        [InlineData(PasswordMode.InlinePassword, CompressionAlgo.Brotli)]
        [InlineData(PasswordMode.PasswordFile, CompressionAlgo.Brotli)]
        [InlineData(PasswordMode.PublicKey, CompressionAlgo.Brotli)]
        [InlineData(PasswordMode.None, CompressionAlgo.Zstd)]
        [InlineData(PasswordMode.InlinePassword, CompressionAlgo.Zstd)]
        [InlineData(PasswordMode.PasswordFile, CompressionAlgo.Zstd)]
        [InlineData(PasswordMode.PublicKey, CompressionAlgo.Zstd)]
        public async Task CompressDecompress(PasswordMode mode, CompressionAlgo algo)
        {
            await _externalDependencies.EnsureAllDependenciesPresent();
            await _dbSetup.EnsureDatabaseExistsAsync(Mode.Benchmark);

            string toCompressFolder = TestFileHelper.NewFolder();
            string compressedFolder = TestFileHelper.NewFolder();
            string decompressedFolder = TestFileHelper.NewFolder();
            string keysFolder = TestFileHelper.NewFolder();

            var data = await TestData.CreateFiles(1, 1024, toCompressFolder);

            OperationSummary resultCompress = await Compress(mode, algo, compressedFolder, keysFolder, data);
            resultCompress.ThrowOnError();

            string warnings = string.Join(Environment.NewLine,
                resultCompress.OperationBlocks
                    .SelectMany(i=>i.StepResults)
                    .Where(s => s.HasWarning)
                    .Select(s => s.Warning)
                );

            Assert.True(resultCompress.IsSuccess, warnings);
            Assert.NotEmpty(resultCompress.OperationBlocks.SelectMany(i => i.StepResults));

            var decomp = new TestData
            {
                Directories = new List<DirectoryInfo>
                {
                    new DirectoryInfo(compressedFolder)
                        .EnumerateDirectories(Environment.MachineName,SearchOption.TopDirectoryOnly)
                        .First()
                }
            };

            OperationSummary resultDecompress = await Decompress(mode, decompressedFolder, keysFolder, decomp);
            resultDecompress.ThrowOnError();

            Assert.True(resultDecompress.IsSuccess);
            Assert.NotEmpty(resultDecompress.OperationBlocks.SelectMany(i => i.StepResults));

            FileInfo src = new DirectoryInfo(toCompressFolder).EnumerateFiles().FirstOrDefault();
            FileInfo dst = new DirectoryInfo(decompressedFolder).EnumerateFiles().FirstOrDefault();

            Assert.True(TestFileHelper.FilesAreEqual(src, dst));
        }

        private async Task<OperationSummary> Decompress(PasswordMode passwordMode, string decompressedFolder, string keysFolder, TestData decomp)
        {
            var decompOption = decomp.GetTccDecompressOption(decompressedFolder);

            decompOption.PasswordOption = _benchmarkOptionHelper.GenerateDecompressPasswordOption(passwordMode, keysFolder);

            var resultDecompress = await _tarCompressCrypt.DecompressAsync(decompOption);
            return resultDecompress;
        }

        private async Task<OperationSummary> Compress(PasswordMode passwordMode, CompressionAlgo algo,
            string compressedFolder, string keysFolder, TestData data)
        {
            CompressOption compressOption = data.GetTccCompressOption(compressedFolder, algo);

            compressOption.PasswordOption = await _benchmarkOptionHelper.GenerateCompressPasswordOption(passwordMode, keysFolder);

            var resultCompress = await _tarCompressCrypt.CompressAsync(compressOption);
            return resultCompress;
        }


    }
}
