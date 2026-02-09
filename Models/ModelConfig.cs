
using System;

namespace OpenClaw.Windows.Models;

public class ModelConfig
{
    public string Name { get; set; } = string.Empty;
    public string RepoUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty; // Usually the folder name
    public double VramRequiredGb { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
    
    // Helper to get full local path
    public string GetLocalPath() => System.IO.Path.Combine(AppContext.BaseDirectory, "Model", FileName);
}
