using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using HtmlAgilityPack;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenClaw.Windows.Services.Tools
{
    public class ReadWebPageTool : IAiTool
    {
        public string Name => "read_web_page";
        public string Description => "Reads the content of a public web page and returns it as text/markdown.";
        public bool IsUnsafe => false; // Reading public web is generally safe

        private readonly HttpClient _httpClient;

        public ReadWebPageTool()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                if (!doc.RootElement.TryGetProperty("url", out var urlProp)) 
                {
                    return "Error: arguments must contain 'url'";
                }
                var url = urlProp.GetString();

                if (string.IsNullOrWhiteSpace(url)) return "Error: url cannot be empty";

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    return "Error: Invalid URL format.";
                }

                var html = await _httpClient.GetStringAsync(uri);
                
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Remove scripts, styles, metadata
                var nodesToRemove = htmlDoc.DocumentNode.SelectNodes("//script|//style|//head|//iframe|//nav|//footer|//noscript");
                if (nodesToRemove != null)
                {
                    foreach (var node in nodesToRemove)
                    {
                        node.Remove();
                    }
                }

                // Extract text from body
                var sb = new StringBuilder();
                var textNodes = htmlDoc.DocumentNode.SelectNodes("//p|//h1|//h2|//h3|//h4|//li|//pre|//code");
                
                if (textNodes != null)
                {
                    foreach (var node in textNodes)
                    {
                        var text = System.Net.WebUtility.HtmlDecode(node.InnerText).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // Add simple formatting hints
                            if (node.Name.StartsWith("h")) sb.AppendLine($"# {text}");
                            else if (node.Name == "li") sb.AppendLine($"- {text}");
                            else sb.AppendLine(text);
                            
                            sb.AppendLine(); 
                        }
                    }
                }

                var final = sb.ToString();
                
                // Truncate to avoid context overflow (approx 10k chars is a safe conservative limit for now)
                if (final.Length > 10000)
                {
                    final = final.Substring(0, 10000) + "\n\n[Content Truncated]";
                }

                if (string.IsNullOrWhiteSpace(final)) return "Page content appeared empty or blocked.";

                return final;
            }
            catch (Exception ex)
            {
                return $"Error reading web page: {ex.Message}";
            }
        }

        public object Parameters => new
        {
            type = "OBJECT",
            properties = new
            {
                url = new
                {
                    type = "STRING",
                    description = "The HTTP/HTTPS URL of the page to read."
                }
            },
            required = new[] { "url" }
        };
    }
}
