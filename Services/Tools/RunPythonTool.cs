using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Executes Python code or scripts within the Super Agent's dedicated venv.
    /// Supports CUDA 12.8 for GPU-accelerated workloads (PyTorch, etc.).
    /// </summary>
    public class RunPythonTool : IAiTool
    {
        private readonly VenvManagerService _venv;

        public string Name => "run_python";
        public string Description => "Executes Python code or a script within the Super Agent's virtual environment (with CUDA 12.8 GPU support). Use 'code' for inline snippets or 'script_path' for .py files.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                code = new
                {
                    type = "string",
                    description = "Inline Python code to execute (e.g. 'import torch; print(torch.cuda.is_available())')"
                },
                script_path = new
                {
                    type = "string",
                    description = "Absolute path to a .py file to execute"
                },
                args = new
                {
                    type = "string",
                    description = "Optional arguments to pass to the script"
                }
            }
        };

        public RunPythonTool(VenvManagerService venv)
        {
            _venv = venv;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                string? code = null;
                string? scriptPath = null;
                string? args = null;

                if (root.TryGetProperty("code", out var codeProp))
                    code = codeProp.GetString();

                if (root.TryGetProperty("script_path", out var pathProp))
                    scriptPath = pathProp.GetString();

                if (root.TryGetProperty("args", out var argsProp))
                    args = argsProp.GetString();

                // Auto-create venv if it doesn't exist
                if (!_venv.VenvExists)
                {
                    var setupResult = await _venv.EnsureVenvAsync();
                    if (!_venv.VenvExists)
                        return $"Failed to setup Python environment: {setupResult}";
                }

                return await _venv.RunPythonAsync(code: code, scriptPath: scriptPath, args: args);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
