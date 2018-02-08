namespace TCC.Lib.Options
{
    /// <summary>
    /// Used for compression
    /// </summary>
    public sealed class PublicKeyPasswordOption : PasswordOption
    {
        public string PublicKeyFile { get; set; }
        public override PasswordMode PasswordMode => PasswordMode.PublicKey;
    }
}