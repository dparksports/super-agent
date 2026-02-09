using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Installs Python packages via pip into the Super Agent's dedicated venv.
    /// HITL approval required (IsUnsafe = true).
    /// </summary>
    public class PipInstallTool : IAiTool
    {
        private readonly VenvManagerService _venv;

        public string Name => "pip_install";
        public string Description => "Installs a Python package via pip into the Super Agent's virtual environment. Can also list installed packages.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                package_name = new
                {
                    type = "string",
                    description = "PyPI package to install (e.g. 'requests', 'pandas', 'torch'). Use 'cuda' to install PyTorch with CUDA 12.8 GPU support."
                },
                action = new
                {
                    type = "string",
                    description = "Action: 'install' (default), 'list', or 'uninstall'",
                    @enum = new[] { "install", "list", "uninstall" }
                }
            },
            required = new[] { "package_name" }
        };

        public PipInstallTool(VenvManagerService venv)
        {
            _venv = venv;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                var packageName = "";
                var action = "install";

                if (root.TryGetProperty("package_name", out var pkgProp))
                    packageName = pkgProp.GetString() ?? "";

                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString() ?? "install";

                switch (action.ToLower())
                {
                    case "list":
                        return await _venv.PipListAsync();

                    case "uninstall":
                        if (string.IsNullOrWhiteSpace(packageName))
                            return "Error: package_name is required for uninstall.";
                        return await _venv.PipInstallAsync($"--yes {packageName}", timeoutMs: 30000);

                    case "install":
                    default:
                        if (string.IsNullOrWhiteSpace(packageName))
                            return "Error: package_name is required.";

                        // Special shortcut: "cuda" installs PyTorch with CUDA 12.8
                        if (packageName.Equals("cuda", StringComparison.OrdinalIgnoreCase))
                            return await _venv.InstallCudaSupportAsync();

                        return await _venv.PipInstallAsync(packageName);
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
