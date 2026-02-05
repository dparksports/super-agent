using System;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    public class GetSystemTimeTool : IAiTool
    {
        public string Name => "get_current_time";
        
        public string Description => "Returns the current local system time.";
        public bool IsUnsafe => false;

        public object Parameters => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArgs)
        {
            return Task.FromResult(DateTime.Now.ToString("g"));
        }
    }
}
