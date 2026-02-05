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
    public ObservableCollection<ChatMessage> Messages { get; } = new();

    public MainPage()
    {
        this.InitializeComponent();
        
        // Resolve AI Service via App Host
        _aiService = ((App)Application.Current).Host.Services.GetRequiredService<IAiService>();
        
        Messages.Add(new ChatMessage("Assistant", "Hello! I am OpenClaw (Windows). I use a Hybrid AI engine (Local + Gemini)."));
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessage();
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
