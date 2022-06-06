using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCC.Lib.Helpers
{
    public static class GoogleAuthHelper
    {
        public static async Task<GoogleCredential> GetGoogleClientAsync(string googleStorageCredential, CancellationToken token)
        {
            if (File.Exists(googleStorageCredential))
            {
                return await GoogleCredential.FromFileAsync(googleStorageCredential, token);
            }
            else
            {
                var decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(googleStorageCredential));
                return GoogleCredential.FromJson(decodedJson);
            }
        }

        public static async Task<StorageClient> GetGoogleStorageClientAsync(string googleStorageCredential, CancellationToken token)
        {
            var credential = await GetGoogleClientAsync(googleStorageCredential, token);
            return await StorageClient.CreateAsync(credential);
        }

    }
}
