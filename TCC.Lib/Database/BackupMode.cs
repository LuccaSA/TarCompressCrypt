using System.ComponentModel;

namespace TCC.Lib.Database
{
    [DefaultValue(Diff)]
    public enum BackupMode
    {
        Diff = 0,
        Full = 1,
    }

}