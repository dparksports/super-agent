using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    public class PowerShellTool : IAiTool
    {
        public string Name => "run_powershell";
        public string Description => "Executes a PowerShell command. USE CAUTION.";
        public bool IsUnsafe => true;
        
        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                command = new
                {
                    type = "string",
                    description = "The PowerShell command to execute."
                }
            },
            required = new[] { "command" }
        };

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;
                
                string command = "";

                if (root.TryGetProperty("command", out var cmdProp))
                    command = cmdProp.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(command))
                    return "Error: command is required.";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(error))
                {
                    return $"Output:\n{output}\nError:\n{error}";
                }

                return string.IsNullOrWhiteSpace(output) ? "Success (No Output)" : output;
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }
    }
}
