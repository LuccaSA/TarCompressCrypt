namespace TCC.Lib.Options
{
    /// <summary>
    /// Store password in a file
    /// </summary>
    public sealed class PasswordFileOption : PasswordOption
    {
        public string PasswordFile { get; set; }
        public override PasswordMode PasswordMode => PasswordMode.PasswordFile;
    }
}