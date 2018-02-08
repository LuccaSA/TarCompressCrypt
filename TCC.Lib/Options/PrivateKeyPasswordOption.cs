namespace TCC.Lib.Options
{
    /// <summary>
    /// Used for decompression
    /// </summary>
    public sealed class PrivateKeyPasswordOption : PasswordOption
    {
        public string PrivateKeyFile { get; set; }
        public override PasswordMode PasswordMode => PasswordMode.PublicKey;
    }
}