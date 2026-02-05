using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public class HybridAiService : IAiService
{
    private readonly OnnxLocalAiService _localService;
    public GoogleGeminiService CloudService { get; }

    public HybridAiService(OnnxLocalAiService localService, GoogleGeminiService cloudService)
    {
        _localService = localService;
        CloudService = cloudService;
    }

    public async IAsyncEnumerable<string> GetStreamingResponseAsync(string systemPrompt, string userPrompt)
    {
        bool isLocal = IsSimpleQuery(userPrompt);
        
        if (isLocal)
        {
            yield return "[Local] 🦞 ";
            await foreach (var chunk in _localService.GetStreamingResponseAsync(systemPrompt, userPrompt))
            {
                yield return chunk;
            }
        }
        else
        {
             yield return "[Gemini] ✨ ";
             await foreach (var chunk in CloudService.GetStreamingResponseAsync(systemPrompt, userPrompt))
             {
                 yield return chunk;
             }
        }
    }

    public async Task RedownloadModelAsync()
    {
        // For hybrid, we assume we want to redownload the local model if possible.
        if (_localService is OnnxLocalAiService local)
        {
            await local.RedownloadModelAsync();
        }
    }

    private bool IsSimpleQuery(string prompt)
    {
        // PoC Logic: Short queries go to Local, Long go to Gemini
        return prompt.Length < 50 || prompt.Contains("time", StringComparison.OrdinalIgnoreCase);
    }
}
