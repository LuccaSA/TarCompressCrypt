using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TCC.Lib.Notification
{
    public class SlackClient
    {
        private readonly ILogger<SlackClient> _logger;

        public SlackClient(ILogger<SlackClient> logger)
        {
            _logger = logger;
        }

        public async Task<SlackResponse> SendSlackMessageAsync(SlackMessage message, string slackSecret)
        {
            try
            {
                var client = new HttpClient {BaseAddress = new Uri("https://slack.com")};
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
                    throw new Exception(ok.StatusCode.ToString());
                }

                var response = JsonConvert.DeserializeObject<SlackResponse>(await ok.Content.ReadAsStringAsync());
                if (!response.Ok)
                {
                    throw new Exception(response.Error);
                }
                return response;
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
            return null;
        }
    }

    public class SlackMessage
    {
        public string Channel { get; set; }
        public string Text { get; set; }
        [JsonProperty(PropertyName = "thread_ts")]
        public string ThreadTs { get; set; }
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

    public class SlackResponse
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string Channel { get; set; }
        public string Ts { get; set; }
        public Message Message { get; set; }
    }

    public class Message
    {
        public string Text { get; set; }
        public string Username { get; set; }
        public string BotId { get; set; }
        public Attachment[] Attachments { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public string Ts { get; set; }
    }
}