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
    private string _apiKey;

    public GoogleGeminiService()
    {
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            try
            {
                var secretPath = System.IO.Path.Combine(AppContext.BaseDirectory, "secrets.json");
                if (System.IO.File.Exists(secretPath))
                {
                    var json = System.IO.File.ReadAllText(secretPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("GeminiApiKey", out var prop))
                    {
                        _apiKey = prop.GetString() ?? "";
                    }
                }
            }
            catch { /* Ignore config errors, api key remains empty */ }
        }
    }
    public string CurrentModel { get; set; } = "gemini-2.0-flash-lite";

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        if (string.IsNullOrEmpty(_apiKey)) return new List<string>();

        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}";
        try 
        {
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var modelsElement))
            {
                foreach (var model in modelsElement.EnumerateArray())
                {
                    var name = model.GetProperty("name").GetString();
                    // name is typically "models/gemini-pro", we want just "gemini-pro" usually, 
                    // or we keep full name and strip "models/" for display? 
                    // The generate API expects "models/gemini-pro" or just "gemini-pro" depending on version.
                    // The error message said "models/gemini-1.5-flash is not found", implying it expects it without "models/" prefix or specific version.
                    // Let's store the full resource name but strip "models/" for the ID we use in valid URLs if the URL structure is .../models/{modelId}:generate
                    
                    if (name != null && name.Contains("gemini") && !name.Contains("embedding") && !name.Contains("robotics") && !name.Contains("competitor"))
                    {
                        models.Add(name.Replace("models/", ""));
                    }
                }
            }
            return models.OrderByDescending(m => m).ToList(); // Newest versions first usually
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to list models: {ex.Message}");
            return new List<string> { "gemini-1.5-flash", "gemini-1.5-pro" }; // Fallback
        }
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

         var url = $"https://generativelanguage.googleapis.com/v1beta/models/{CurrentModel}:generateContent?key={_apiKey}";
         
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
