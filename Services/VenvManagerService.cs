using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services
{
    /// <summary>
    /// Manages a dedicated Python virtual environment for the Super Agent.
    /// Supports CUDA 12.8 (cu128) for GPU-accelerated workloads.
    /// </summary>
    public class VenvManagerService
    {
        private readonly string _venvPath;
        private readonly string _pythonExe;

        public string VenvPath => _venvPath;
        public string PythonExe => _pythonExe;
        public bool VenvExists => Directory.Exists(_venvPath) && File.Exists(_pythonExe);

        public VenvManagerService()
        {
            // Store venv in %APPDATA%\SuperAgent\venv
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _venvPath = Path.Combine(appData, "SuperAgent", "venv");
            _pythonExe = Path.Combine(_venvPath, "Scripts", "python.exe");
        }

        /// <summary>
        /// Ensures the venv exists. Creates it if not present.
        /// </summary>
        public async Task<string> EnsureVenvAsync()
        {
            if (VenvExists)
                return $"Venv already exists at {_venvPath}";

            // Create venv using system python
            var result = await RunProcessAsync("python", $"-m venv \"{_venvPath}\"");

            if (!VenvExists)
                return $"Failed to create venv: {result}";

            // Upgrade pip inside the venv
            await RunProcessAsync(_pythonExe, "-m pip install --upgrade pip");

            return $"Venv created at {_venvPath}";
        }

        /// <summary>
        /// Installs PyTorch with CUDA 12.8 (cu128) support into the venv.
        /// </summary>
        public async Task<string> InstallCudaSupportAsync()
        {
            if (!VenvExists)
            {
                var setupResult = await EnsureVenvAsync();
                if (!VenvExists) return setupResult;
            }

            var result = await RunProcessAsync(_pythonExe,
                "-m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu128",
                timeoutMs: 300000); // 5 min timeout for large download

            return result;
        }

        /// <summary>
        /// Installs a pip package into the venv.
        /// </summary>
        public async Task<string> PipInstallAsync(string packageName, int timeoutMs = 120000)
        {
            if (!VenvExists)
            {
                var setupResult = await EnsureVenvAsync();
                if (!VenvExists) return $"Venv not available: {setupResult}";
            }

            return await RunProcessAsync(_pythonExe, $"-m pip install {packageName}", timeoutMs: timeoutMs);
        }

        /// <summary>
        /// Lists installed packages in the venv.
        /// </summary>
        public async Task<string> PipListAsync()
        {
            if (!VenvExists)
                return "Venv does not exist. Use ensure_venv first.";

            return await RunProcessAsync(_pythonExe, "-m pip list");
        }

        /// <summary>
        /// Runs a Python script or inline code within the venv.
        /// </summary>
        public async Task<string> RunPythonAsync(string? code = null, string? scriptPath = null, string? args = null, int timeoutMs = 60000)
        {
            if (!VenvExists)
            {
                var setupResult = await EnsureVenvAsync();
                if (!VenvExists) return $"Venv not available: {setupResult}";
            }

            string arguments;
            if (!string.IsNullOrWhiteSpace(scriptPath))
            {
                arguments = $"\"{scriptPath}\"";
                if (!string.IsNullOrWhiteSpace(args))
                    arguments += $" {args}";
            }
            else if (!string.IsNullOrWhiteSpace(code))
            {
                // Use -c for inline code
                var escaped = code.Replace("\"", "\\\"");
                arguments = $"-c \"{escaped}\"";
            }
            else
            {
                return "Error: Either 'code' or 'script_path' must be provided.";
            }

            return await RunProcessAsync(_pythonExe, arguments, timeoutMs: timeoutMs);
        }

        private async Task<string> RunProcessAsync(string fileName, string arguments, int timeoutMs = 60000)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    return $"Error: Process timed out after {timeoutMs / 1000}s.";
                }

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                    return $"Error (Exit Code {process.ExitCode}):\n{error}\n{output}".Trim();

                return string.IsNullOrWhiteSpace(output) ? "Success (No Output)" : output.Trim();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
