using System.Collections.Generic;
using System.CommandLine;

namespace TCC.Parser
{
    public static class RemoteStorageOptions
    {
        public static IEnumerable<Option> GetCommonOptions()
        {
            yield return new Option<string>(new[] { "--azBlobUrl" }, "Azure blob storage URL");
            yield return new Option<string>(new[] { "--azBlobContainer" }, "Azure blob storage container id");
            yield return new Option<string>(new[] { "--azBlobSaS" }, "Azure blob storage SaS token");
            yield return new Option<int?>(new[] { "--azThread" }, "Azure blob maximum parallel threads");
            yield return new Option<string>(new[] { "--googleStorageBucketName" }, "Google Cloud Storage destination bucket");
            yield return new Option<string>(new[] { "--googleStorageCredential" }, "Google Cloud Storage credential json, either full path or base64");
            yield return new Option<string>(new[] { "--s3AccessKeyId" }, "S3 Access Key ID");
            yield return new Option<string>(new[] { "--s3Region" }, "S3 Region");
            yield return new Option<string>(new[] { "--s3SecretAcessKey" }, "S3 Access Key Secret");
            yield return new Option<string>(new[] { "--s3Host" }, "S3 Host");
            yield return new Option<string>(new[] { "--s3BucketName" }, "S3 destination bucket");

        }
    }
}