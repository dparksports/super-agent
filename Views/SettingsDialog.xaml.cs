using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System;

namespace OpenClaw.Windows.Views
{
    public sealed partial class SettingsDialog : ContentDialog
    {
        public SettingsDialog()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                var enabled = Services.SettingsHelper.Get<bool>("AnalyticsEnabled", true);
                if (AnalyticsToggle != null)
                {
                    AnalyticsToggle.IsOn = enabled;
                    AnalyticsToggle.Toggled += AnalyticsToggle_Toggled;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSettings Error: {ex}");
            }
        }

        private void AnalyticsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                Services.SettingsHelper.Set("AnalyticsEnabled", toggle.IsOn);
            }
        }

        private async void ResetEulaButton_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                Services.SettingsHelper.Remove("EulaAccepted");
                
                // Log for debugging
                var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: EULA Reset Clicked. Removed 'EulaAccepted' key.\n");
                
                // Give feedback
                ResetEulaButton.Content = "EULA Reset!";
                ResetEulaButton.IsEnabled = false;
                
                await System.Threading.Tasks.Task.Delay(2000);
                
                ResetEulaButton.Content = "Reset EULA acceptance";
                ResetEulaButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                 var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                 System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Reset Error: {ex}\n");
                 
                 ResetEulaButton.Content = "Error Resetting!";
                 await System.Threading.Tasks.Task.Delay(2000);
                 ResetEulaButton.Content = "Reset EULA acceptance";
            }
    }
    }
}
