namespace TCC.Lib.Options
{
    /// <summary>
    /// Inline password
    /// </summary>
    public sealed class InlinePasswordOption : PasswordOption
    {
        public string Password { get; set; }
        public override PasswordMode PasswordMode => PasswordMode.InlinePassword;
    }
}