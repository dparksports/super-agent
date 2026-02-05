using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public class HybridAiService : IAiService
{
    public async IAsyncEnumerable<string> GetStreamingResponseAsync(string systemPrompt, string userPrompt)
    {
        bool isLocal = IsSimpleQuery(userPrompt);
        string prefix = isLocal ? "[Local LLM] 🦞 " : "[Gemini Cloud] ✨ ";

        // Simulate streaming delay
        await Task.Delay(300);

        yield return prefix;
        
        string responseText = isLocal 
            ? $"Processed locally: {userPrompt}" 
            : $"Processed by Gemini: {userPrompt} (Reasoning...)";

        var words = responseText.Split(' ');
        foreach (var word in words)
        {
            await Task.Delay(50); // Typing effect
            yield return word + " ";
        }
    }

    private bool IsSimpleQuery(string query)
    {
        // PoC Logic: Short queries go to Local, Long go to Gemini
        return query.Length < 20 || query.Contains("time", StringComparison.OrdinalIgnoreCase);
    }
}
