namespace TCC.Lib.Blocks
{
    public class EncryptionKey
    {
        public EncryptionKey(string key, string keyCrypted)
        {
            Key = key;
            KeyCrypted = keyCrypted;
        }

        public string Key { get; }
        public string KeyCrypted { get; }
    }
}