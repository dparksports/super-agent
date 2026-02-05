using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public class SidecarService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // MOCK: Launch Node.js process here
        // var startInfo = new ProcessStartInfo("node", "dist/index.js gateway");
        // Process.Start(startInfo);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
