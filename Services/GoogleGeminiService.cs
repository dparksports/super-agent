using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenClaw.Windows.Services;

public class GoogleGeminiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string ModelId = "gemini-1.5-flash"; // Cost-effective and fast

    public GoogleGeminiService()
    {
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
    }

    public async IAsyncEnumerable<string> GetStreamingResponseAsync(string systemPrompt, string userPrompt)
    {
        // For V1 stability, we will fetch the full response and yield it.
        // True streaming requires parsing the complex JSON array stream from Google.
        
        var fullText = await GenerateContentAsync(systemPrompt + "\n\n" + userPrompt);
        
        // Simulate streaming for UI smoothness
        var words = fullText.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(10); // Tiny delay for effect
        }
    }
    
    // We'll overload this to use the non-streaming endpoint for stability in V1
    public async Task<string> GenerateContentAsync(string prompt)
    {
         if (string.IsNullOrEmpty(_apiKey)) return "GEMINI_API_KEY missing";

         var url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelId}:generateContent?key={_apiKey}";
         
         var requestBody = new
         {
             contents = new[]
             {
                 new { role = "user", parts = new[] { new { text = prompt } } }
             }
         };
         
         var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));
         var json = await response.Content.ReadAsStringAsync();
         
         if (!response.IsSuccessStatusCode) return $"Error: {json}";

         try 
         {
             using var doc = JsonDocument.Parse(json);
             var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
             return text ?? "";
         }
         catch 
         {
             return "Error parsing Gemini response.";
         }
    }

    Task IAiService.RedownloadModelAsync() => Task.CompletedTask; // Not applicable
}
