namespace TCC.Lib.Blocks
{
    public enum BlockMode
    {
        /// <summary>
        /// Each explicit file or folder in given input files/folders yields an archive
        /// </summary>
        Explicit,
        /// <summary>
        /// Foreach file or folder in given inputs files/folders, we yield an archive
        /// </summary>
        Individual,
    }
}