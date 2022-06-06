namespace TCC.Lib.Options
{
    public interface ISlackOption
    {
        string SlackChannel { get; }
        string SlackSecret { get;}
        string BucketName { get; }
        bool SlackOnlyOnError { get; }
    }
}