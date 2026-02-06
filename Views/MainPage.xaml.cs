using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Windows.Services;
using OpenClaw.Windows.Models;

namespace OpenClaw.Windows.Views;

public sealed partial class MainPage : Page
{
    private readonly IAiService _aiService;
    private readonly ISlackService _slackService;
    private readonly AgentOrchestrator _agent;
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainPage()
    {
        this.InitializeComponent();
        
        // Resolve Services via App Host
        var services = ((App)Application.Current).Host.Services;
        _aiService = services.GetRequiredService<IAiService>();
        _slackService = services.GetRequiredService<ISlackService>();
        _agent = services.GetRequiredService<AgentOrchestrator>();
        
        if (_aiService is OnnxLocalAiService localService)
        {
            localService.DownloadProgressChanged += OnDownloadProgressChanged;
        }

        _slackService.MessageReceived += OnSlackMessageReceived;
        _ = _slackService.ConnectAsync();

        _ = LoadHistoryAsync();
        _ = LoadGeminiModels();
    }
    
    private async Task LoadHistoryAsync()
    {
        var history = await _agent.LoadHistoryAsync();
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var msg in history)
            {
                Messages.Add(msg);
            }
            if (Messages.Count == 0)
            {
                Messages.Add(new ChatMessage("Assistant", "Hello! I am Super Agent 🦸‍♂️. How can I help you today?"));
            }
        });
    }
    
    private async Task LoadGeminiModels()
    {
        if (_aiService is HybridAiService hybrid)
        {
            var models = await hybrid.CloudService.GetAvailableModelsAsync();
            DispatcherQueue.TryEnqueue(() =>
            {
                ModelSelector.ItemsSource = models;
                if (models.Count > 0)
                {
                    // Prefer Flash Lite, then 2.0 Flash, then 1.5 Flash
                    var preferred = models.FirstOrDefault(m => m.Contains("flash-lite"))
                                 ?? models.FirstOrDefault(m => m == "gemini-2.0-flash") 
                                 ?? models.FirstOrDefault(m => m.Contains("flash")) 
                                 ?? models.FirstOrDefault();
                                 
                    if (preferred != null)
                    {
                        hybrid.CloudService.CurrentModel = preferred;
                        ModelSelector.SelectedItem = preferred;
                    }
                }
            });
        }
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is string model && _aiService is HybridAiService hybrid)
        {
            hybrid.CloudService.CurrentModel = model;
        }
    }

    private void OnSlackMessageReceived(object? sender, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Messages.Add(new ChatMessage("Slack", message));
        });
    }

    private void OnDownloadProgressChanged(string status, double progress)
    {
        // Must run on UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            if (DownloadStatusPanel.Visibility == Visibility.Collapsed)
            {
                DownloadStatusPanel.Visibility = Visibility.Visible;
                InputBox.IsEnabled = false;
                SendButton.IsEnabled = false;
            }

            DownloadStatusText.Text = $"{status} ({progress:F0}%)";
            DownloadProgressBar.Value = progress;

            if (progress >= 100)
            {
                DownloadStatusPanel.Visibility = Visibility.Collapsed;
                InputBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                Messages.Add(new ChatMessage("System", "Model downloaded successfully! You can now chat."));
            }
        });
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string content)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(content);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }

    private async void SkillsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SkillsDialog();
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SettingsDialog();
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings Error: {ex}");
                try
                {
                    var logPath = System.IO.Path.Combine(global::Windows.Storage.ApplicationData.Current.LocalFolder.Path, "settings_error.log");
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: {ex}\n\n");
                }
                catch
                {
                    // Ignore logging errors to prevent cascading crashes
                }
            }
        }

        private async void RedownloadButton_Click(object sender, RoutedEventArgs e)
    {
        InputBox.IsEnabled = false;
        SendButton.IsEnabled = false;
        Messages.Add(new ChatMessage("System", "Starting model re-download..."));
        
        try
        {
            await _aiService.RedownloadModelAsync();
            Messages.Add(new ChatMessage("System", "Re-download complete."));
        }
        catch (Exception ex)
        {
             Messages.Add(new ChatMessage("System", $"Error: {ex.Message}"));
        }
        finally
        {
             InputBox.IsEnabled = true;
             SendButton.IsEnabled = true;
             DownloadStatusPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void InputBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter)
        {
            await SendMessage();
        }
    }

    private string? _base64Image;

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        _base64Image = null;
        ImagePreview.Source = null;
        ImagePreviewPanel.Visibility = Visibility.Collapsed;
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        await _agent.ClearHistoryAsync();
        Messages.Clear();
        Messages.Add(new ChatMessage("Assistant", "History cleared. Ready for a new task! ✨"));
    }

    private async void InputGrid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
    }

    private async void InputGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
            {
                var fileType = file.ContentType;
                if (fileType.StartsWith("image/"))
                {
                    await ProcessStorageFile(file);
                }
            }
        }
    }

    private async void InputBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        var dataPackageView = global::Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (dataPackageView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap))
        {
            e.Handled = true; // Prevent pasting "Bitmap" text
            var reference = await dataPackageView.GetBitmapAsync();
            using var stream = await reference.OpenReadAsync();
            await SetImagePreview(stream);
        }
        else if (dataPackageView.Contains(global::Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await dataPackageView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is global::Windows.Storage.StorageFile file)
            {
                 if (file.ContentType.StartsWith("image/"))
                 {
                     e.Handled = true;
                     await ProcessStorageFile(file);
                 }
            }
        }
    }

    private async Task ProcessStorageFile(global::Windows.Storage.StorageFile file)
    {
        using var stream = await file.OpenAsync(global::Windows.Storage.FileAccessMode.Read);
        await SetImagePreview(stream);
    }

    private async Task SetImagePreview(global::Windows.Storage.Streams.IRandomAccessStream stream)
    {
        // 1. Display in UI
        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        await bitmap.SetSourceAsync(stream);
        ImagePreview.Source = bitmap;
        ImagePreviewPanel.Visibility = Visibility.Visible;

        // 2. Convert to Base64 for API
        // Reset stream position
        stream.Seek(0);
        var reader = new global::Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[stream.Size];
        reader.ReadBytes(bytes);
        _base64Image = Convert.ToBase64String(bytes);
    }

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text) && _base64Image == null) return;

        string userText = InputBox.Text;
        string? imageToSend = _base64Image;
        
        InputBox.Text = "";
        ClearImage_Click(this, new RoutedEventArgs()); // Clear UI image state immediately
        
        Messages.Add(new ChatMessage("User", userText) 
        { 
             // Ideally we show the image in the chat history too, but ChatMessage needs an Image property.
             // For now, we append a marker.
             Content = userText + (imageToSend != null ? " [🖼️ Image Attached]" : "")
        });

        var assistantMsg = new ChatMessage("Assistant", "");
        Messages.Add(assistantMsg);

        try 
        {
            // Use AgentOrchestrator Loop
            // Pass the image if present
            await foreach (var chunk in _agent.ChatAsync(userText, Messages, imageToSend))
            {
                assistantMsg.Content += chunk;
            }
        }
        catch (Exception ex)
        {
            assistantMsg.Content += $"\n[Error] {ex.Message}";
        }
    }
}
