using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenClaw.Windows.Models
{
    // These classes mirror the Gemini API JSON structure for "contents"
    
    public class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user"; // user, model, function

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("functionCall")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiFunctionResponse? FunctionResponse { get; set; }

        [JsonPropertyName("inlineData")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiInlineData? InlineData { get; set; }
    }

    public class GeminiInlineData
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = "image/jpeg";

        [JsonPropertyName("data")]
        public string Data { get; set; } = "";
    }

    public class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("args")]
        public object Args { get; set; } = new { };
    }

    public class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("response")]
        public object Response { get; set; } = new { };
    }
}
