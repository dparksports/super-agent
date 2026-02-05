using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public interface IAiService
{
    IAsyncEnumerable<string> GetStreamingResponseAsync(string systemPrompt, string userPrompt);
}
