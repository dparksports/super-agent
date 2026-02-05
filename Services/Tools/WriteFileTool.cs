using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    public class WriteFileTool : IAiTool
    {
        public string Name => "write_file";
        public string Description => "Writes text to a file in the user's Documents folder. Overwrites if exists. Requires 'fileName' and 'content'.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                fileName = new
                {
                    type = "string",
                    description = "The name of the file to write to (e.g., 'notes.txt')."
                },
                content = new
                {
                    type = "string",
                    description = "The text content to write to the file."
                }
            },
            required = new[] { "fileName", "content" }
        };

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;
                
                string fileName = "";
                string content = "";

                if (root.TryGetProperty("fileName", out var fileNameProp))
                    fileName = fileNameProp.GetString() ?? "";
                
                if (root.TryGetProperty("content", out var contentProp))
                    content = contentProp.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(fileName))
                    return "Error: fileName is required.";

                // Security: Restrict to Documents folder
                var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var fullPath = Path.Combine(docsPath, fileName);
                var canonicalPath = Path.GetFullPath(fullPath);

                if (!canonicalPath.StartsWith(docsPath, StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: Access denied. Can only write to Documents folder.";
                }

                await File.WriteAllTextAsync(canonicalPath, content);
                return $"Success: Wrote to {fileName}";
            }
            catch (Exception ex)
            {
                return $"Error writing file: {ex.Message}";
            }
        }
    }
}
