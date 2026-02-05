using System;
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
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainPage()
    {
        this.InitializeComponent();
        
        // Resolve Services via App Host
        var services = ((App)Application.Current).Host.Services;
        _aiService = services.GetRequiredService<IAiService>();
        _slackService = services.GetRequiredService<ISlackService>();
        
        if (_aiService is OnnxLocalAiService localService)
        {
            localService.DownloadProgressChanged += OnDownloadProgressChanged;
        }

        _slackService.MessageReceived += OnSlackMessageReceived;
        _ = _slackService.ConnectAsync();

        Messages.Add(new ChatMessage("Assistant", "Hello! I am OpenGemini. I use a Hybrid AI engine (Local + Gemini)."));
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

    private async Task SendMessage()
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text)) return;

        string userText = InputBox.Text;
        InputBox.Text = "";
        
        Messages.Add(new ChatMessage("User", userText));

        var assistantMsg = new ChatMessage("Assistant", "");
        Messages.Add(assistantMsg);

        try 
        {
            await foreach (var chunk in _aiService.GetStreamingResponseAsync("", userText))
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
