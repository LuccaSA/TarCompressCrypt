namespace TCC.Lib.Options
{
    public sealed class NoPasswordOption : PasswordOption
    {
        public static readonly NoPasswordOption Nop = new NoPasswordOption();

        public override PasswordMode PasswordMode => PasswordMode.None;
    }
}