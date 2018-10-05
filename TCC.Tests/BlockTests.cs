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

        [Fact]
        public async Task DiscoverExplicitBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption()
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.Explicit
            };

            var blocks = BlockHelper.PreprareCompressBlocks(compressOption);
            var fi = new FileInfo(_root);
            Assert.Single(blocks);
            Assert.Equal(fi.Name, blocks.First().Source.Trim('"'));
        }

        [Fact]
        public async Task DiscoverIndividualBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption()
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.Individual
            };

            var blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            Assert.Equal(6, blocks.Count);
        }

        [Fact]
        public async Task DiscoverEachFileBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption()
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.EachFile
            };

            var blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            Assert.Equal(3, blocks.Count);
        }

        [Fact]
        public async Task DiscoverEachFileRecursiveBlocks()
        {
            await Prepare();
            var compressOption = new CompressOption()
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.EachFileRecursive
            };

            var blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            Assert.Equal(6, blocks.Count);
        }

        public void Dispose()
        {
            _fi11.TryDeleteFileWithRetry();
            _fi12.TryDeleteFileWithRetry();
            _fi13.TryDeleteFileWithRetry();
            _fi1.TryDeleteFileWithRetry();
            _fi2.TryDeleteFileWithRetry();
            _fi3.TryDeleteFileWithRetry();

            Directory.Delete(_fo1);
            Directory.Delete(_fo2);
            Directory.Delete(_fo3);
            Directory.Delete(_root);
        }
    }
}
