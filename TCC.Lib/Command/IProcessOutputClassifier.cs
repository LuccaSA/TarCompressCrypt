namespace TCC.Lib.Command
{
    public interface IProcessOutputClassifier
    {
        bool IsIgnorable(string line);
        bool IsInfo(string line);
    }
}