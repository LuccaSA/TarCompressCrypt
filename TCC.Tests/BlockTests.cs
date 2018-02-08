using System;
using System.IO;
using System.Linq;
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
        private readonly string _fi11;
        private readonly string _fi12;
        private readonly string _fi13;
        private readonly string _fi1;
        private readonly string _fi2;
        private readonly string _fi3;

        public CompressBlockTests()
        {
            _root = TestHelper.NewFolder();
            _target = TestHelper.NewFolder();

            _fo1 = TestHelper.NewFolder(_root);
            _fo2 = TestHelper.NewFolder(_root);
            _fo3 = TestHelper.NewFolder(_root);

            _fi11 = TestHelper.NewFile(_fo1);
            _fi12 = TestHelper.NewFile(_fo1);
            _fi13 = TestHelper.NewFile(_fo1);

            _fi1 = TestHelper.NewFile(_root);
            _fi2 = TestHelper.NewFile(_root);
            _fi3 = TestHelper.NewFile(_root);
        }

        [Fact]
        public void DiscoverExplicitBlocks()
        {
            var compressOption = new CompressOption()
            {
                SourceDirOrFile = _root,
                DestinationDir = _target,
                BlockMode = BlockMode.Explicit
            };

            var blocks = BlockHelper.PreprareCompressBlocks(compressOption);

            Assert.Single(blocks);
            Assert.Equal(_root, blocks.First().Source);
        }

        [Fact]
        public void DiscoverIndividualBlocks()
        {
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
        public void DiscoverEachFileBlocks()
        {
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
        public void DiscoverEachFileRecursiveBlocks()
        {
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
