using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TCC.Notification
{
    public class SlackNotifier
    {
        public static async Task SendSlackMessageAsync(SlackMessage message, string slackSecret)
        {
            try
            {
                var client = new HttpClient();
                client.BaseAddress = new Uri("https://slack.com");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", slackSecret);

                var json = JsonConvert.SerializeObject(message, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var ok = await client.PostAsync("api/chat.postMessage", content);
                if (!ok.IsSuccessStatusCode)
                {
                    //log.LogError(ok.ReasonPhrase);
                }
                else
                {
                    var response = await ok.Content.ReadAsStringAsync();
                    //log.LogInformation(response);
                }
            }
            catch (Exception)
            {
                //log.LogError(e, "slack");
            }
        }
    }

    public class SlackReturn
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
    }


    public class SlackMessage
    {
        public string Channel { get; set; }
        public string Text { get; set; }
        public List<Attachment> Attachments { get; set; }

    }

    public class Attachment
    {
        public string Fallback { get; set; }
        public string Color { get; set; }
        public string Pretext { get; set; }

        public string Title { get; set; }
        public string TitleLink { get; set; }
        public string Text { get; set; }
        public Field[] Fields { get; set; }
    }

    public class Field
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public bool Short { get; set; }
    }

   
    public enum AlertLevel
    {
        Info,
        Warning,
        Error
    }

    public static class AlertHelper
    {
        public static string ToSlackColor(this AlertLevel alertLevel)
        {
            switch (alertLevel)
            {
                case AlertLevel.Info:
                    return "good";
                case AlertLevel.Warning:
                    return "warning";
                case AlertLevel.Error:
                    return "danger";
                default:
                    throw new ArgumentOutOfRangeException(nameof(alertLevel), alertLevel, null);
            }
        }
    }
}
