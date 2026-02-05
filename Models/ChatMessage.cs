using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenClaw.Windows.Models;

public partial class ChatMessage : ObservableObject
{
    private string role;
    public string Role
    {
        get => role;
        set => SetProperty(ref role, value);
    }

    private string content;
    public string Content
    {
        get => content;
        set => SetProperty(ref content, value);
    }

    public ChatMessage(string role, string content)
    {
        this.role = role ?? "";
        this.content = content ?? "";
        this.Timestamp = System.DateTime.Now;
    }

    private string toolCallId;
    public string ToolCallId
    {
        get => toolCallId;
        set => SetProperty(ref toolCallId, value);
    }
    
    public System.DateTime Timestamp { get; set; }
}
