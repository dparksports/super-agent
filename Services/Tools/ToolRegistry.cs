using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Windows.Services.Tools
{
    public class ToolRegistry
    {
        private readonly Dictionary<string, IAiTool> _tools = new(StringComparer.OrdinalIgnoreCase);

        public ToolRegistry(IEnumerable<IAiTool> tools)
        {
            foreach (var tool in tools)
            {
                RegisterTool(tool);
            }
        }

        public void RegisterTool(IAiTool tool)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            _tools[tool.Name] = tool;
        }

        public IAiTool? GetTool(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _tools.TryGetValue(name, out var tool) ? tool : null;
        }

        public object GetGeminiFunctionDeclarations()
        {
            var functionDeclarations = _tools.Values.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = tool.Parameters
            }).ToList();

            // The Gemini API expects a root object with "tools" which contains "function_declarations"
            // However, usually we pass the function_declarations list to the API client or build the tool config.
            // Based on typical usage, we return the list of declarations. 
            // The caller will wrap it in { tools: [ { function_declarations: [...] } ] } if needed, 
            // or pass it directly if constructing the request object.
            // Let's return the list directly for flexibility.
            
            return functionDeclarations;
        }
    }
}
