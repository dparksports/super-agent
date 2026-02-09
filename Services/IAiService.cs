using System.Collections.Generic;
using System.Threading.Tasks;
using OpenClaw.Windows.Models;

namespace OpenClaw.Windows.Services;

public interface IAiService
{
    IAsyncEnumerable<OpenClaw.Windows.Models.AgentResponse> GetStreamingResponseAsync(string systemPrompt, string userPrompt);
    Task<AgentResponse> GenerateContentAsync(string prompt);
    Task<AgentResponse> GenerateContentAsync(List<GeminiContent> history);
    Task RedownloadModelAsync();
    Task SwitchModelAsync(string modelPath);
}
