using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public class HybridAiService : IAiService
{
    private readonly OnnxLocalAiService _localService;
    private readonly SafetyService _safetyService;
    public GoogleGeminiService CloudService { get; }

    public HybridAiService(OnnxLocalAiService localService, GoogleGeminiService cloudService, SafetyService safetyService)
    {
        _localService = localService;
        CloudService = cloudService;
        _safetyService = safetyService;
    }

    public async IAsyncEnumerable<OpenClaw.Windows.Models.AgentResponse> GetStreamingResponseAsync(string systemPrompt, string userPrompt)
    {
        // 1. Safety Check
        if (!_safetyService.IsPromptSafe(userPrompt))
        {
             yield return new OpenClaw.Windows.Models.AgentResponse { Text = "⚠️ Request blocked by Safety Protocols (Injection Detected)." };
             yield break;
        }

        // 2. PII Scrubbing
        string safePrompt = _safetyService.ScrubPii(userPrompt);

        bool costSaving = SettingsHelper.Get<bool>("CostSavingMode", true);
        bool isLocal = IsSimpleQuery(safePrompt) || costSaving;
        
        if (isLocal)
        {
            yield return new OpenClaw.Windows.Models.AgentResponse { Text = "[Local] 🦞 " };
            await foreach (var chunk in _localService.GetStreamingResponseAsync(systemPrompt, safePrompt))
            {
                yield return chunk;
            }
        }
        else
        {
             yield return new OpenClaw.Windows.Models.AgentResponse { Text = "[Gemini] ✨ " };
             await foreach (var chunk in CloudService.GetStreamingResponseAsync(systemPrompt, safePrompt))
             {
                 yield return chunk;
             }
        }
    }

    public async Task<OpenClaw.Windows.Models.AgentResponse> GenerateContentAsync(string prompt)
    {
        bool costSaving = SettingsHelper.Get<bool>("CostSavingMode", true);
        // Simple heuristic: if short or explicit cost saving, try local.
        if (costSaving || IsSimpleQuery(prompt))
        {
            try 
            {
                var response = await _localService.GenerateContentAsync(prompt);
                response.Text = "[Local] " + response.Text;
                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Hybrid] Local failed, falling back to cloud: {ex.Message}");
                // Fallback to cloud
            }
        }

        return await CloudService.GenerateContentAsync(prompt);
    }

    public async Task<OpenClaw.Windows.Models.AgentResponse> GenerateContentAsync(List<OpenClaw.Windows.Models.GeminiContent> history)
    {
        // For complex history/agent tasks, we usually prefer Cloud unless strictly cost saving.
        // We need to implement proper agentic local support later.
        // For now, if cost saving is ON, we might try local but local model context window is small (4k).
        
        bool costSaving = SettingsHelper.Get<bool>("CostSavingMode", false);
        
        // Check if history is small enough for local
        if (costSaving)
        {
             try 
             {
                 // Convert GeminiContent to simple prompt for Local if needed, 
                 // BUT OnnxLocalAiService needs to support history. 
                 // For now, let's keep complex agent tasks on Cloud unless user forces it.
                 // We will fallback to cloud for robust function calling which Phi-3 supports but our implementation might not yet.
                 System.Diagnostics.Debug.WriteLine("[Hybrid] Complex task - defaulting to Cloud for reliability for now.");
             }
             catch {}
        }

        return await CloudService.GenerateContentAsync(history);
    }

    public async Task RedownloadModelAsync()
    {
        // For hybrid, we assume we want to redownload the local model if possible.
        if (_localService is OnnxLocalAiService local)
        {
            await local.RedownloadModelAsync();
        }
    }

    public async Task SwitchModelAsync(string modelPath)
    {
        if (_localService is OnnxLocalAiService local)
        {
            await local.SwitchModelAsync(modelPath);
        }
    }

    private bool IsSimpleQuery(string prompt)
    {
        // PoC Logic: Short queries go to Local, Long go to Gemini
        return prompt.Length < 100 || prompt.Contains("time", StringComparison.OrdinalIgnoreCase);
    }
}
