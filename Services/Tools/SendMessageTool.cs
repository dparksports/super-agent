using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Sends messages across 90+ platforms using the Apprise Python library.
    /// Supports Slack, Discord, Telegram, Email (SMTP), Teams, Pushover, and more.
    /// https://github.com/caronc/apprise
    /// </summary>
    public class SendMessageTool : IAiTool
    {
        private readonly VenvManagerService _venv;

        public string Name => "send_message";
        public string Description => "Sends a message via Apprise (90+ platforms: Email, Slack, Discord, Telegram, Teams, SMS, Pushover, etc.). Use 'list_services' to see supported platforms or 'setup_info' for configuration help.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                service_url = new
                {
                    type = "string",
                    description = "Apprise service URL (e.g. 'mailto://user:pass@gmail.com', 'slack://token/channel', 'discord://webhook_id/webhook_token', 'tgram://bot_token/chat_id'). Multiple URLs can be comma-separated."
                },
                message = new
                {
                    type = "string",
                    description = "Message body to send"
                },
                title = new
                {
                    type = "string",
                    description = "Optional message title/subject"
                },
                action = new
                {
                    type = "string",
                    description = "Action: 'send' (default), 'list_services', 'setup_info'",
                    @enum = new[] { "send", "list_services", "setup_info" }
                }
            }
        };

        public SendMessageTool(VenvManagerService venv)
        {
            _venv = venv;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                var serviceUrl = "";
                var message = "";
                var title = "";
                var action = "send";

                if (root.TryGetProperty("service_url", out var urlProp))
                    serviceUrl = urlProp.GetString() ?? "";
                if (root.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString() ?? "";
                if (root.TryGetProperty("title", out var titleProp))
                    title = titleProp.GetString() ?? "";
                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString() ?? "send";

                switch (action.ToLower())
                {
                    case "list_services":
                        return GetSupportedServices();

                    case "setup_info":
                        return GetSetupInfo();

                    case "send":
                    default:
                        if (string.IsNullOrWhiteSpace(serviceUrl))
                        {
                            // Check for saved service URLs in config
                            var configUrl = LoadSavedServiceUrl();
                            if (string.IsNullOrWhiteSpace(configUrl))
                                return "Error: service_url is required. Use 'setup_info' for help, or 'list_services' for supported platforms.";
                            serviceUrl = configUrl;
                        }

                        if (string.IsNullOrWhiteSpace(message))
                            return "Error: message is required.";

                        return await SendViaAppriseAsync(serviceUrl, message, title);
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private async Task<string> SendViaAppriseAsync(string serviceUrl, string message, string title)
        {
            var scriptPath = GetAppriseScript();

            var scriptArgs = JsonSerializer.Serialize(new
            {
                service_url = serviceUrl,
                message,
                title
            });

            var escapedArgs = scriptArgs.Replace("\"", "\\\"");

            return await _venv.RunPythonAsync(
                scriptPath: scriptPath,
                args: $"--json \"{escapedArgs}\"",
                timeoutMs: 30000);
        }

        private string? LoadSavedServiceUrl()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configPath = Path.Combine(appData, "SuperAgent", "messaging_config.json");

            if (!File.Exists(configPath)) return null;

            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("default_service_url", out var urlProp))
                    return urlProp.GetString();
            }
            catch { }

            return null;
        }

        private string GetSupportedServices()
        {
            return @"## Apprise Supported Services (Common)

| Platform | URL Format |
|---|---|
| **Email (SMTP)** | `mailto://user:pass@gmail.com` |
| **Slack** | `slack://TokenA/TokenB/TokenC/Channel` |
| **Discord** | `discord://WebhookID/WebhookToken` |
| **Telegram** | `tgram://BotToken/ChatID` |
| **Microsoft Teams** | `msteams://TokenA/TokenB/TokenC` |
| **Pushover** | `pover://UserKey@Token` |
| **Twilio SMS** | `twilio://AccountSID:AuthToken@FromNum/ToNum` |
| **WhatsApp** | `whatsapp://AccountSID:AuthToken@FromNum/ToNum` |
| **Matrix** | `matrix://user:pass@hostname/room` |
| **Gotify** | `gotify://hostname/token` |
| **ntfy** | `ntfy://topic` |
| **Pushbullet** | `pbul://AccessToken` |

Full list: https://github.com/caronc/apprise/wiki

### Save Default Config:
Create `%APPDATA%\SuperAgent\messaging_config.json`:
```json
{
    ""default_service_url"": ""mailto://user:pass@gmail.com""
}
```
";
        }

        private string GetSetupInfo()
        {
            return @"## Messaging Setup

### Quick Start (Email):
```
service_url: mailto://your_email:your_app_password@gmail.com
```
For Gmail, use an App Password (not your regular password):
1. Go to myaccount.google.com → Security → 2-Step Verification → App Passwords
2. Generate a new app password
3. Use it in the URL above

### Quick Start (Discord):
1. Go to your Discord server → Settings → Integrations → Webhooks
2. Create a webhook, copy the URL
3. Convert: `https://discord.com/api/webhooks/WEBHOOK_ID/WEBHOOK_TOKEN`
   → `discord://WEBHOOK_ID/WEBHOOK_TOKEN`

### Quick Start (Telegram):
1. Message @BotFather on Telegram, create a bot
2. Get your bot token and chat ID
3. Use: `tgram://BOT_TOKEN/CHAT_ID`

### Apprise will be auto-installed in the venv on first use.
";
        }

        private string GetAppriseScript()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var scriptDir = Path.Combine(appData, "SuperAgent", "scripts");
            Directory.CreateDirectory(scriptDir);

            var scriptPath = Path.Combine(scriptDir, "send_message.py");

            if (!File.Exists(scriptPath))
            {
                File.WriteAllText(scriptPath, SendMessagePythonScript);
            }

            return scriptPath;
        }

        private const string SendMessagePythonScript = @"
import sys, json, argparse

def ensure_apprise():
    try:
        import apprise
        return True
    except ImportError:
        import subprocess
        subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'apprise'])
        return True

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--json', required=True, help='JSON args')
    args = parser.parse_args()

    params = json.loads(args.json)
    service_url = params['service_url']
    message = params['message']
    title = params.get('title', 'Super Agent 🦸‍♂️')

    ensure_apprise()
    import apprise

    apobj = apprise.Apprise()

    # Support comma-separated URLs for multi-platform delivery
    for url in service_url.split(','):
        url = url.strip()
        if url:
            apobj.add(url)

    result = apobj.notify(
        title=title,
        body=message,
    )

    if result:
        print(f'Message sent successfully via {len(service_url.split("",""))} service(s)')
    else:
        print('Failed to send message. Check your service URL and credentials.')

if __name__ == '__main__':
    main()
";
    }
}
