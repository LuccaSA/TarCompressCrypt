using System.ComponentModel;

namespace TCC.Lib.Database
{
    [DefaultValue(Diff)]
    public enum BackupMode
    {
        Diff,
        Full,
    }

}