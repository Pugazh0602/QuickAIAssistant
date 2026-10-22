using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QuickAIAssistant
{
    internal sealed class GroqClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";

        public async Task<string> AskAsync(string apiKey, string prompt, string selectedText, string imageDataUrl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Add your Groq API key in Settings first.");

            var serializer = new JavaScriptSerializer { MaxJsonLength = 15 * 1024 * 1024 };
            var messages = new object[2];
            messages[0] = new { role = "system", content = "You are Quick AI, a concise and helpful Windows assistant. Follow the user's instruction. If asked for code, provide complete usable code and explain briefly. Do not claim to have executed code." };

            string userInstruction = string.IsNullOrWhiteSpace(prompt)
                ? "Explain this simply with an example."
                : prompt.Trim();

            if (!string.IsNullOrWhiteSpace(imageDataUrl))
            {
                // Vision support requires a vision-capable model configured in Settings.
                messages[1] = new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userInstruction + "\nPlease inspect the attached image." },
                        new { type = "image_url", image_url = new { url = imageDataUrl } }
                    }
                };
            }
            else
            {
                string content = userInstruction;
                if (!string.IsNullOrWhiteSpace(selectedText))
                    content += "\n\nSelected content:\n" + selectedText;
                messages[1] = new { role = "user", content = content };
            }

            var body = new
            {
                model = "openai/gpt-oss-120b",
                messages = messages,
                temperature = 0.3,
                max_tokens = 1800
            };

            string json = serializer.Serialize(body);
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string detail = responseText;
                        try
                        {
                            var errorObj = serializer.DeserializeObject(responseText) as System.Collections.Generic.Dictionary<string, object>;
                            if (errorObj != null && errorObj.ContainsKey("error"))
                            {
                                var err = errorObj["error"] as System.Collections.Generic.Dictionary<string, object>;
                                if (err != null && err.ContainsKey("message")) detail = Convert.ToString(err["message"]);
                            }
                        }
                        catch { }
                        throw new InvalidOperationException("Groq API error (" + (int)response.StatusCode + "): " + detail);
                    }

                    var root = serializer.DeserializeObject(responseText) as System.Collections.Generic.Dictionary<string, object>;
                    if (root == null || !root.ContainsKey("choices"))
                        throw new InvalidOperationException("The API returned an unexpected response.");
                    var choices = root["choices"] as object[];
                    if (choices == null || choices.Length == 0)
                        throw new InvalidOperationException("Groq returned an empty response.");
                    var choice = choices[0] as System.Collections.Generic.Dictionary<string, object>;
                    var message = choice != null && choice.ContainsKey("message") ? choice["message"] as System.Collections.Generic.Dictionary<string, object> : null;
                    if (message == null || !message.ContainsKey("content"))
                        throw new InvalidOperationException("Groq returned no message content.");
                    return Convert.ToString(message["content"]);
                }
            }
        }
    }
}
