using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    public class ReadFileTool : IAiTool
    {
        public string Name => "read_file";

        public string Description => "Reads the contents of a text file from the user's Documents folder. The path should be relative to the Documents folder or an absolute path within it.";
        public bool IsUnsafe => false;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                file_path = new
                {
                    type = "string",
                    description = "The path to the file to read."
                }
            },
            required = new[] { "file_path" }
        };

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                if (!doc.RootElement.TryGetProperty("file_path", out var pathProp))
                {
                    return "Error: Missing 'file_path' argument.";
                }

                string inputPath = pathProp.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(inputPath))
                {
                    return "Error: File path cannot be empty.";
                }

                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fullPath = Path.GetFullPath(Path.Combine(docsPath, inputPath));

                // Security Check: Access Control
                if (!fullPath.StartsWith(docsPath, StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: Access denied. You can only read files within the Documents folder.";
                }

                if (!File.Exists(fullPath))
                {
                    return $"Error: File not found at {fullPath}";
                }

                // Check file size using FileInfo to avoid reading huge files into memory?
                // For now, let's just read it asynchronously.
                return await File.ReadAllTextAsync(fullPath);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }
    }
}
