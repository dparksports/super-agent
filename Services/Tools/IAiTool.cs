using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    public interface IAiTool
    {
        string Name { get; }
        string Description { get; }
        object Parameters { get; }
        bool IsUnsafe { get; }
        Task<string> ExecuteAsync(string jsonArgs);
    }
}
