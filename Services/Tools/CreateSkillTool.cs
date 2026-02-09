using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Allows the agent to create new skills (tools) for itself at runtime.
    /// Writes SKILL.md + script files to the Skills directory and triggers 
    /// SkillService to reload, making the new tool immediately available.
    /// This is the "meta-skill" — the agent teaching itself new capabilities.
    /// </summary>
    public class CreateSkillTool : IAiTool
    {
        private readonly Skills.SkillService _skillService;
        private readonly ToolRegistry _toolRegistry;

        public string Name => "create_skill";
        public string Description => "Creates a new dynamic skill (tool) that the agent can use in the future. Writes a SKILL.md manifest and script file, then reloads the skill registry. Use 'skill_format' action to learn the required file format.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'create' (default) or 'skill_format' (show the required SKILL.md format)",
                    @enum = new[] { "create", "skill_format" }
                },
                skill_name = new
                {
                    type = "string",
                    description = "Name for the new skill (used as folder name, e.g. 'check_weather')"
                },
                description = new
                {
                    type = "string",
                    description = "Description of what the skill does"
                },
                language = new
                {
                    type = "string",
                    description = "Script language: 'python' or 'powershell'",
                    @enum = new[] { "python", "powershell" }
                },
                script_content = new
                {
                    type = "string",
                    description = "The full source code of the script"
                },
                parameters_schema = new
                {
                    type = "string",
                    description = "JSON Schema defining the tool's parameters (optional but recommended)"
                },
                author = new
                {
                    type = "string",
                    description = "Author name (default: 'Super Agent')"
                },
                version = new
                {
                    type = "string",
                    description = "Version string (default: '1.0.0')"
                }
            }
        };

        public CreateSkillTool(Skills.SkillService skillService, ToolRegistry toolRegistry)
        {
            _skillService = skillService;
            _toolRegistry = toolRegistry;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                var action = "create";
                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString() ?? "create";

                if (action == "skill_format")
                    return GetSkillFormatInfo();

                // Extract parameters
                var skillName = "";
                var description = "";
                var language = "python";
                var scriptContent = "";
                var parametersSchema = "";
                var author = "Super Agent 🦸‍♂️";
                var version = "1.0.0";

                if (root.TryGetProperty("skill_name", out var nameProp))
                    skillName = nameProp.GetString() ?? "";
                if (root.TryGetProperty("description", out var descProp))
                    description = descProp.GetString() ?? "";
                if (root.TryGetProperty("language", out var langProp))
                    language = langProp.GetString() ?? "python";
                if (root.TryGetProperty("script_content", out var scriptProp))
                    scriptContent = scriptProp.GetString() ?? "";
                if (root.TryGetProperty("parameters_schema", out var schemaProp))
                    parametersSchema = schemaProp.GetString() ?? "";
                if (root.TryGetProperty("author", out var authorProp))
                    author = authorProp.GetString() ?? "Super Agent 🦸‍♂️";
                if (root.TryGetProperty("version", out var versionProp))
                    version = versionProp.GetString() ?? "1.0.0";

                // Validate
                if (string.IsNullOrWhiteSpace(skillName))
                    return "Error: skill_name is required.";
                if (string.IsNullOrWhiteSpace(description))
                    return "Error: description is required.";
                if (string.IsNullOrWhiteSpace(scriptContent))
                    return "Error: script_content is required.";

                // Sanitize skill name for folder
                var safeName = skillName.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");

                // Create skill directory
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var skillDir = Path.Combine(appData, "OpenClaw", "Skills", safeName);
                Directory.CreateDirectory(skillDir);

                // Write SKILL.md
                var skillMd = BuildSkillMd(skillName, description, author, version, parametersSchema);
                await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), skillMd);

                // Write script file
                var scriptExt = language.ToLower() == "powershell" ? "ps1" : "py";
                var scriptFile = $"script.{scriptExt}";
                await File.WriteAllTextAsync(Path.Combine(skillDir, scriptFile), scriptContent);

                // Reload skill registry
                await _skillService.RefreshSkillsAsync();
                foreach (var skill in _skillService.GetSkills())
                {
                    _toolRegistry.RegisterTool(new Skills.SkillAdapter(skill));
                }

                return $"✅ Skill '{skillName}' created successfully!\n" +
                       $"📁 Location: {skillDir}\n" +
                       $"📄 Files: SKILL.md + {scriptFile}\n" +
                       $"🔄 Skill registry reloaded. The new tool is now available.";
            }
            catch (Exception ex)
            {
                return $"Error creating skill: {ex.Message}";
            }
        }

        private string BuildSkillMd(string name, string description, string author, string version, string? parametersSchema)
        {
            var md = $@"---
name: {name}
description: {description}
author: {author}
version: {version}
---

# {name}

{description}

Created by {author} (v{version}).
";

            if (!string.IsNullOrWhiteSpace(parametersSchema))
            {
                md += $@"
<tool_def>
{parametersSchema}
</tool_def>
";
            }

            return md;
        }

        private string GetSkillFormatInfo()
        {
            return @"## Skill File Format

A skill is a folder inside `%APPDATA%\OpenClaw\Skills\{skill_name}\` containing:

### 1. SKILL.md (Required)
```markdown
---
name: my_cool_skill
description: Does something awesome
author: Super Agent 🦸‍♂️
version: 1.0.0
---

# My Cool Skill

A longer description of what this skill does.

<tool_def>
{
  ""type"": ""object"",
  ""properties"": {
    ""input_text"": { ""type"": ""string"", ""description"": ""Text to process"" },
    ""mode"": { ""type"": ""string"", ""enum"": [""fast"", ""accurate""], ""description"": ""Processing mode"" }
  },
  ""required"": [""input_text""]
}
</tool_def>
```

### 2. Script File (Required)
- `script.py` for Python skills
- `script.ps1` for PowerShell skills

The script receives parameters as named arguments:
- **Python**: `python script.py --input_text ""hello"" --mode ""fast""`
- **PowerShell**: `powershell script.ps1 -input_text ""hello"" -mode ""fast""`

### 3. SKILL.md Sections:
- **YAML Frontmatter** (between `---`): name, description, author, version
- **`<tool_def>`** block: JSON Schema for parameters (tells the AI what args to pass)
- **Markdown body**: Documentation (optional but helpful)

### Example Python Script:
```python
import argparse
parser = argparse.ArgumentParser()
parser.add_argument('--input_text', required=True)
parser.add_argument('--mode', default='fast')
args = parser.parse_args()
print(f'Processed: {args.input_text} in {args.mode} mode')
```
";
        }
    }
}
