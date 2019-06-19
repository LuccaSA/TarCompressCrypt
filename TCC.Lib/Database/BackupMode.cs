using System.ComponentModel;

namespace TCC.Lib.Database
{
    [DefaultValue(Diff)]
    public enum BackupMode
    {
        Diff,
        Full,
    }

    [DefaultValue(Diff)]
    public enum RestoreMode
    {
        Diff,
        Full,
    }
}