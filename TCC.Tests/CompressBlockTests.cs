using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TCC.Lib.Benchmark;
using TCC.Lib.Blocks;
using TCC.Lib.Helpers;
using TCC.Lib.Options;
using Xunit;

namespace TCC.Tests
{
    public sealed class CompressBlockTests : IDisposable
    {
        private readonly string _root;
        private readonly string _target;
        private readonly string _fo1;
        private readonly string _fo2;
        private readonly string _fo3;
        private string _fi11;
        private string _fi12;
        private string _fi13;
        private string _fi1;
        private string _fi2;
        private string _fi3;

        public CompressBlockTests()
        {
            _root = TestFileHelper.NewFolder();
            _target = TestFileHelper.NewFolder();

            _fo1 = TestFileHelper.NewFolder(_root);
            _fo2 = TestFileHelper.NewFolder(_root);
            _fo3 = TestFileHelper.NewFolder(_root);
        }

        private async Task Prepare()
        {
            _fi11 = await TestFileHelper.NewFile(_fo1);
            _fi12 = await TestFileHelper.NewFile(_fo1);
            _fi13 = await TestFileHelper.NewFile(_fo1);

            _fi1 = await TestFileHelper.NewFile(_root);
            _fi2 = await TestFileHelper.NewFile(_root);
            _fi3 = await TestFileHelper.NewFile(_root);
        }

        private async Task Cleanup()
        {
            await _fi11.TryDeleteFileWithRetryAsync();
            await _fi12.TryDeleteFileWithRetryAsync();
            await _fi13.TryDeleteFileWithRetryAsync();
            await _fi1.TryDeleteFileWithRetryAsync();
            await _fi2.TryDeleteFileWithRetryAsync();
            await _fi3.TryDeleteFileWithRetryAsync();
        }

        [Fact]
        public async Task DiscoverExplicitBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.Explicit
            };
            var compFolder = new CompressionFolderProvider(new DirectoryInfo(compressOption.DestinationDir), compressOption.FolderPerDay);
            var blocks = compressOption.GenerateCompressBlocks(compFolder);
            var fi = new FileInfo(_root);
            Assert.Single(blocks);
            Assert.Equal(fi.Name, blocks.First().Source.Trim('"'));
            await Cleanup();
        }

        [Fact]
        public async Task DiscoverIndividualBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.Individual
            };
            var compFolder = new CompressionFolderProvider(new DirectoryInfo(compressOption.DestinationDir), compressOption.FolderPerDay);
            var blocks = compressOption.GenerateCompressBlocks(compFolder);

            Assert.Equal(6, blocks.Count());
            await Cleanup();
        }
         
         

        public void Dispose()
        {
            Directory.Delete(_fo1);
            Directory.Delete(_fo2);
            Directory.Delete(_fo3);
            Directory.Delete(_root);
        }
    }
}
