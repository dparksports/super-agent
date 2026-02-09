using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection; // Added for GetRequiredService

namespace OpenClaw.Windows.Views
{
    public sealed partial class SettingsDialog : ContentDialog
    {
        private static readonly string GuidePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SuperAgent", "guide.md");

        private System.Collections.ObjectModel.ObservableCollection<OpenClaw.Windows.Models.ModelConfig> _models;
        private Services.OnnxLocalAiService _aiService;
        private Services.HardwareService _hardwareService;

        public SettingsDialog()
        {
            this.InitializeComponent();
            _models = new System.Collections.ObjectModel.ObservableCollection<OpenClaw.Windows.Models.ModelConfig>();
            _aiService = App.Current.Host.Services.GetRequiredService<Services.OnnxLocalAiService>();
            _hardwareService = new Services.HardwareService();
            
            LoadSettings();
            LoadModels();
        }

        private void LoadModels()
        {
            // Seed Models
            
            // 1. Gemma 3 27B (Default)
            _models.Add(new OpenClaw.Windows.Models.ModelConfig 
            { 
                Name = "Gemma 3 27B (Google)", 
                Description = "Multimodal capable. Requires ~16GB VRAM. (Manual Setup Required)",
                FileName = "google\\gemma-3-27b-it-onnx-int4", // Nested folder structure
                RepoUrl = "MANUAL", // Special flag
                VramRequiredGb = 16.0
            });

            _models.Add(new OpenClaw.Windows.Models.ModelConfig 
            { 
                Name = "DeepSeek-R1-Distill-Qwen-32B", 
                Description = "High reasoning capability. Requires ~20GB VRAM.",
                FileName = "DeepSeek-R1-Distill-Qwen-32B",
                RepoUrl = "https://huggingface.co/onnx-community/DeepSeek-R1-Distill-Qwen-32B-ONNX",
                VramRequiredGb = 20.0
            });
            
            _models.Add(new OpenClaw.Windows.Models.ModelConfig 
            { 
                Name = "DeepSeek-R1-Distill-Llama-8B", 
                Description = "Efficient & fast. Requires ~6GB VRAM.",
                FileName = "DeepSeek-R1-Distill-Llama-8B",
                RepoUrl = "https://huggingface.co/onnx-community/DeepSeek-R1-Distill-Llama-8B-ONNX",
                VramRequiredGb = 6.0
            });

            _models.Add(new OpenClaw.Windows.Models.ModelConfig 
            { 
                Name = "Phi-4-mini-instruct", 
                Description = "Microsoft's latest small model. Requires ~8GB VRAM.",
                FileName = "Phi-4-mini-instruct",
                RepoUrl = "https://huggingface.co/microsoft/Phi-4-mini-instruct-onnx",
                VramRequiredGb = 8.0
            });

            _models.Add(new OpenClaw.Windows.Models.ModelConfig 
            { 
                Name = "DeepSeek-R1-Distill-Llama-70B", 
                Description = "Massive reasoning model. Requires ~48GB VRAM (Dual GPU/Enterprise).",
                FileName = "DeepSeek-R1-Distill-Llama-70B",
                RepoUrl = "https://huggingface.co/onnx-community/DeepSeek-R1-Distill-Llama-70B-ONNX",
                VramRequiredGb = 48.0
            });

            ModelComboBox.ItemsSource = _models;
            
            // Set selection based on saved settings or default to Gemma 3
            var savedModelName = Services.SettingsHelper.Get<string>("SelectedModelName", "google\\gemma-3-27b-it-onnx-int4");
            
            // Fallback for old settings using just folder name
            if (!savedModelName.Contains("\\") && savedModelName == "DeepSeek-R1-Distill-Qwen-32B")
            {
                 // Keep preference if it was DeepSeek, but ensure we match the FileName
            }
            else if (string.IsNullOrEmpty(savedModelName))
            {
                savedModelName = "google\\gemma-3-27b-it-onnx-int4";
            }

            var selected = System.Linq.Enumerable.FirstOrDefault(_models, m => m.FileName == savedModelName);
            ModelComboBox.SelectedItem = selected ?? _models[0];
            
            UpdateVramStats();
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
             if (ModelComboBox.SelectedItem is OpenClaw.Windows.Models.ModelConfig model)
             {
                 Services.SettingsHelper.Set("SelectedModelName", model.FileName);
                 
                 // Check if model exists
                 bool exists = Directory.Exists(model.GetLocalPath());
                 DownloadModelButton.Content = exists ? "Switch Model" : "Download & Load";
                 
                 UpdateVramStats();
                 
                 // If it exists, we can technically switch immediately, but let's let user click button to confirm
             }
        }
        
        private void UpdateVramStats()
        {
            if (ModelComboBox.SelectedItem is OpenClaw.Windows.Models.ModelConfig model)
            {
                double totalVram = _hardwareService.GetTotalVramGb();
                double required = model.VramRequiredGb;

                VramStatsText.Text = $"{required} GB / {totalVram} GB";
                
                // Avoid divide by zero
                double percentage = totalVram > 0 ? (required / totalVram) * 100 : 0;
                
                VramProgressBar.Value = percentage;
                
                if (required > totalVram)
                {
                    VramProgressBar.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    VramWarningText.Text = "⚠️ Warning: This model requires more VRAM than available. It may crash or run very slowly on CPU/System RAM.";
                    VramWarningText.Visibility = Visibility.Visible;
                }
                else if (percentage > 80)
                {
                    VramProgressBar.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    VramWarningText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    VramProgressBar.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    VramWarningText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
        {
             if (ModelComboBox.SelectedItem is OpenClaw.Windows.Models.ModelConfig model)
             {
                 DownloadModelButton.IsEnabled = false;
                 
                 // Manual Setup Check
                 if (model.RepoUrl == "MANUAL")
                 {
                     var result = await new ContentDialog
                     {
                         Title = "Manual Setup Required",
                         Content = "This model (Gemma 3) requires manual conversion using the provided Python script.\n\n" +
                                   "1. Click 'Open Script Folder' below.\n" +
                                   "2. Run 'convert_model.py' in a terminal.\n" +
                                   "3. Wait for conversion to finish.\n" +
                                   "4. Click 'Switch Model' here once done.",
                         PrimaryButtonText = "Open Script Folder",
                         CloseButtonText = "Cancel",
                         XamlRoot = this.XamlRoot
                     }.ShowAsync();

                     if (result == ContentDialogResult.Primary)
                     {
                         // Open explorer to Utilities folder
                         string utilitiesPath = Path.Combine(AppContext.BaseDirectory, "Utilities");
                         if (!Directory.Exists(utilitiesPath)) Directory.CreateDirectory(utilitiesPath);
                         
                         System.Diagnostics.Process.Start("explorer.exe", utilitiesPath);
                     }
                     
                     DownloadModelButton.IsEnabled = true;
                     return;
                 }


                 DownloadProgressBar.Visibility = Visibility.Visible;
                 DownloadStatusText.Visibility = Visibility.Visible;
                 
                 // Switch model logic
                 try 
                 {
                     string modelPath = model.GetLocalPath();
                     
                     if (!Directory.Exists(modelPath) || !File.Exists(Path.Combine(modelPath, "model.onnx")))
                     {
                         // Download
                         var downloadService = new Services.ModelDownloadService();
                         
                         var statusProgress = new Progress<string>(s => DownloadStatusText.Text = s);
                         var downloadProgress = new Progress<double>(d => DownloadProgressBar.Value = d);
                         
                         await downloadService.DownloadModelAsync(model, statusProgress, downloadProgress);
                     }
                     
                     // Now Load/Switch
                     DownloadStatusText.Text = "Loading Model...";
                     await _aiService.SwitchModelAsync(modelPath);
                     
                     DownloadStatusText.Text = "Model Loaded Successfully!";
                     DownloadModelButton.Content = "Switch Model"; // Update text
                 }
                 catch (Exception ex)
                 {
                     DownloadStatusText.Text = $"Error: {ex.Message}";
                 }
                 finally
                 {
                     DownloadModelButton.IsEnabled = true;
                     // Leave status visible for feedback
                 }
             }
        }

        private void LoadSettings()
        {
            try
            {
                // HITL Toggle (default: ON for safety)
                var hitlEnabled = Services.SettingsHelper.Get<bool>("HitlEnabled", true);
                if (HitlToggle != null)
                {
                    HitlToggle.IsOn = hitlEnabled;
                    HitlToggle.Toggled += HitlToggle_Toggled;
                }

                // Analytics Toggle
                var analyticsEnabled = Services.SettingsHelper.Get<bool>("AnalyticsEnabled", true);
                if (AnalyticsToggle != null)
                {
                    AnalyticsToggle.IsOn = analyticsEnabled;
                    AnalyticsToggle.Toggled += AnalyticsToggle_Toggled;
                }

                // Guide status
                UpdateGuideStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSettings Error: {ex}");
            }
        }

        private void HitlToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                Services.SettingsHelper.Set("HitlEnabled", toggle.IsOn);
            }
        }

        private void AnalyticsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                Services.SettingsHelper.Set("AnalyticsEnabled", toggle.IsOn);
            }
        }

        private async void EditGuideButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create guide.md with defaults if it doesn't exist
                var dir = Path.GetDirectoryName(GuidePath)!;
                Directory.CreateDirectory(dir);

                if (!File.Exists(GuidePath))
                {
                    File.WriteAllText(GuidePath, DefaultGuideContent);
                }

                // Open in default editor (Notepad)
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = GuidePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processInfo);

                EditGuideButton.Content = "✅ Opened in editor!";
                UpdateGuideStatus();

                await System.Threading.Tasks.Task.Delay(2000);
                EditGuideButton.Content = "📝 Edit Agent Guide";
            }
            catch (Exception ex)
            {
                EditGuideButton.Content = $"Error: {ex.Message}";
                await System.Threading.Tasks.Task.Delay(2000);
                EditGuideButton.Content = "📝 Edit Agent Guide";
            }
        }

        private void UpdateGuideStatus()
        {
            if (GuideStatusText != null)
            {
                if (File.Exists(GuidePath))
                {
                    var info = new FileInfo(GuidePath);
                    GuideStatusText.Text = $"📄 Guide loaded ({info.Length} bytes) — {GuidePath}";
                }
                else
                {
                    GuideStatusText.Text = "No guide.md found. Click above to create one.";
                }
            }
        }

        private async void ResetEulaButton_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                Services.SettingsHelper.Remove("EulaAccepted");
                
                var logPath = Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                File.AppendAllText(logPath, $"{DateTime.Now}: EULA Reset Clicked. Removed 'EulaAccepted' key.\n");
                
                ResetEulaButton.Content = "EULA Reset!";
                ResetEulaButton.IsEnabled = false;
                
                await System.Threading.Tasks.Task.Delay(2000);
                
                ResetEulaButton.Content = "Reset EULA acceptance";
                ResetEulaButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                 var logPath = Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                 File.AppendAllText(logPath, $"{DateTime.Now}: Reset Error: {ex}\n");
                 
                 ResetEulaButton.Content = "Error Resetting!";
                 await System.Threading.Tasks.Task.Delay(2000);
                 ResetEulaButton.Content = "Reset EULA acceptance";
            }
    }

        private const string DefaultGuideContent = @"# Agent Guide 🦸‍♂️

This file controls what your Super Agent should and shouldn't do.
Edit this file to set boundaries and preferences — like a parent setting rules.

## General Rules
- Always explain what you are about to do before doing it
- Be transparent about errors and failures
- Prefer local/private solutions over cloud services when possible

## Things You CAN Do
- Write and run Python/PowerShell scripts to automate tasks
- Browse the web to research information
- Install Python packages when needed for a task
- Send me messages and notifications
- Create new skills to learn new capabilities

## Things You Should NOT Do
- Never delete important files without explicit confirmation
- Never share personal information with external services
- Never make purchases or financial transactions
- Never modify system-critical files (registry, boot config, etc.)
- Never send messages to contacts I haven't approved

## Preferences
- My preferred language: English
- My timezone: Pacific Time
- When in doubt, ask me first

## Contact Info (for messaging/calls)
- Owner phone: (not configured)
- Preferred messaging: (not configured)
";
    }
}
