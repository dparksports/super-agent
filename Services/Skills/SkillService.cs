using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Skills
{
    public class SkillService
    {
        private readonly string _skillsRoot;
        private readonly System.Collections.ObjectModel.ObservableCollection<ISkill> _loadedSkills = new();

        public SkillService()
        {
            _skillsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClaw", "Skills");
            Directory.CreateDirectory(_skillsRoot);
        }

        public System.Collections.ObjectModel.ObservableCollection<ISkill> LoadedSkills => _loadedSkills;

        public IEnumerable<ISkill> GetSkills() => _loadedSkills;

        public async Task RefreshSkillsAsync()
        {
            _loadedSkills.Clear();
            var directories = Directory.GetDirectories(_skillsRoot);

            foreach (var dir in directories)
            {
                var skill = await LoadSkillFromDirectoryAsync(dir);
                if (skill != null)
                {
                    _loadedSkills.Add(skill);
                }
            }
        }

        private async Task<ISkill?> LoadSkillFromDirectoryAsync(string directory)
        {
            var skillFile = Path.Combine(directory, "SKILL.md");
            if (!File.Exists(skillFile)) return null;

            // 1. Parse Metadata
            var metadata = await ParseSkillMetadataAsync(skillFile);
            if (string.IsNullOrEmpty(metadata.Name)) return null;

            // 2. Determine Runtime
            var psScript = Path.Combine(directory, "script.ps1");
            var pyScript = Path.Combine(directory, "script.py");

            if (File.Exists(psScript))
            {
                var skill = new PowerShellSkill(metadata.Name, metadata.Description, psScript);
                if (!string.IsNullOrEmpty(metadata.ParametersJson)) 
                {
                    // Reflection or internal setter would be ideal, but for now we rely on constructor if possible
                    // Or we add a property to ISkill/Base implementation?
                    // PowerShellSkill default constructor generates default params. 
                    // Let's modify PowerShellSkill access or assumption.
                    // Actually, ISkill is an interface. PowerShellSkill is the concrete class.
                    // I need to update PowerShellSkill to accept paramsJson in constructor or setter.
                    // For now, I'll assume I can add it to the constructor in next step.
                    // But wait, ISkill doesn't enforce set.
                }
                return new PowerShellSkill(metadata.Name, metadata.Description, psScript, metadata.ParametersJson);
            }
            else if (File.Exists(pyScript))
            {
                 return new PythonSkill(metadata.Name, metadata.Description, pyScript);
            }

            return null;
        }

        private async Task<SkillManifest> ParseSkillMetadataAsync(string path)
        {
            var manifest = new SkillManifest { Name = "Unknown", Description = "No description." };
            
            try 
            {
                var content = await File.ReadAllTextAsync(path);
                var match = Regex.Match(content, @"^---\s*\r?\n(.*?)\r?\n---\s*\r?\n", RegexOptions.Singleline);
                
                if (match.Success)
                {
                    var yaml = match.Groups[1].Value;
                    manifest.Name = ExtractYamlValue(yaml, "name");
                    manifest.Description = ExtractYamlValue(yaml, "description");
                    manifest.Author = ExtractYamlValue(yaml, "author");
                    manifest.Version = ExtractYamlValue(yaml, "version");
                }

                // Extract <tool_def> JSON block if present
                var toolDefStart = content.IndexOf("<tool_def>");
                var toolDefEnd = content.IndexOf("</tool_def>");
                if (toolDefStart != -1 && toolDefEnd != -1 && toolDefEnd > toolDefStart)
                {
                    var json = content.Substring(toolDefStart + 10, toolDefEnd - (toolDefStart + 10)).Trim();
                    manifest.ParametersJson = json;
                }
            }
            catch { }
            
            if (string.IsNullOrEmpty(manifest.Name) || manifest.Name == "Unknown")
            {
                 manifest.Name = Path.GetFileName(Path.GetDirectoryName(path)) ?? "Unknown";
            }

            return manifest;
        }
        
        internal class SkillManifest
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Author { get; set; } = "";
            public string Version { get; set; } = "";
            public string? ParametersJson { get; set; }
        }

        private string ExtractYamlValue(string yaml, string key)
        {
            var match = Regex.Match(yaml, $@"^{key}:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }
    }
}
