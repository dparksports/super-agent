using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenClaw.Windows.Models;

public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string role;

    [ObservableProperty]
    private string content;

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
}
