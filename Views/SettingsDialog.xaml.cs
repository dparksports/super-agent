using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System;
using System.IO;

namespace OpenClaw.Windows.Views
{
    public sealed partial class SettingsDialog : ContentDialog
    {
        private static readonly string GuidePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SuperAgent", "guide.md");

        public SettingsDialog()
        {
            this.InitializeComponent();
            LoadSettings();
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
