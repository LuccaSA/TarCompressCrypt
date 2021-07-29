namespace TCC.Lib.Storage
{
    public class UploadResponse
    {
        public string RemoteFilePath { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
}