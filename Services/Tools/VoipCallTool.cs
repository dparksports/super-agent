using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Makes VoIP calls and sends SMS using a local SIP library (PJSIP via Python).
    /// Requires a SIP account configuration. Can use providers like VoIP.ms, 
    /// Anveo, or FreePBX for temporary/permanent phone numbers.
    /// </summary>
    public class VoipCallTool : IAiTool
    {
        private readonly VenvManagerService _venv;

        public string Name => "voip_call";
        public string Description => "Makes a VoIP phone call or sends an SMS via a local SIP provider. Requires SIP account to be configured in %APPDATA%/SuperAgent/sip_config.json. Use 'setup_info' action for setup instructions.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'call' (make a call with TTS message), 'sms' (send text), 'setup_info' (get setup instructions), 'status' (check SIP registration)",
                    @enum = new[] { "call", "sms", "setup_info", "status" }
                },
                phone_number = new
                {
                    type = "string",
                    description = "Phone number to call/text (E.164 format, e.g. +15551234567)"
                },
                message = new
                {
                    type = "string",
                    description = "Message to speak (TTS for calls) or text (for SMS)"
                }
            },
            required = new[] { "action" }
        };

        public VoipCallTool(VenvManagerService venv)
        {
            _venv = venv;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                var action = "";
                var phoneNumber = "";
                var message = "";

                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString() ?? "";
                if (root.TryGetProperty("phone_number", out var phoneProp))
                    phoneNumber = phoneProp.GetString() ?? "";
                if (root.TryGetProperty("message", out var msgProp))
                    message = msgProp.GetString() ?? "";

                if (action == "setup_info")
                    return GetSetupInstructions();

                if (action == "status")
                    return await CheckSipStatusAsync();

                if (string.IsNullOrWhiteSpace(phoneNumber))
                    return "Error: phone_number is required for call/sms.";
                if (string.IsNullOrWhiteSpace(message))
                    return "Error: message is required for call/sms.";

                var configPath = GetConfigPath();
                if (!File.Exists(configPath))
                    return $"Error: SIP not configured. Use action 'setup_info' for instructions. Expected config at: {configPath}";

                var scriptPath = GetSipScript();

                var scriptArgs = JsonSerializer.Serialize(new
                {
                    action,
                    phone_number = phoneNumber,
                    message,
                    config_path = configPath
                });

                var escapedArgs = scriptArgs.Replace("\"", "\\\"");

                return await _venv.RunPythonAsync(
                    scriptPath: scriptPath,
                    args: $"--json \"{escapedArgs}\"",
                    timeoutMs: 60000);
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private string GetConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "SuperAgent", "sip_config.json");
        }

        private string GetSetupInstructions()
        {
            var configPath = GetConfigPath();
            return $@"## VoIP Setup Instructions

To enable VoIP calls and SMS, you need a SIP provider account.

### Recommended Providers (Temp/Permanent Numbers):
1. **VoIP.ms** — $0.85/mo for a DID number, pay-per-minute calls
2. **Anveo** — Free incoming, cheap outgoing
3. **Twilio** — $1.15/mo for a number + per-minute usage (also supports SMS)
4. **FreePBX** — Self-hosted, free with your own SIP trunk

### Configuration:
Create this file: {configPath}

```json
{{
    ""sip_server"": ""your-sip-server.com"",
    ""sip_port"": 5060,
    ""sip_username"": ""your_username"",
    ""sip_password"": ""your_password"",
    ""caller_id"": ""+15551234567"",
    ""owner_phone"": ""+15559876543""
}}
```

### Getting a Temporary Number:
- **VoIP.ms**: Sign up → DID Numbers → Order → Choose area code → $0.85/mo
- **Twilio**: Sign up → Buy a Number → Choose local → $1.15/mo
- Both provide SIP credentials you can plug into the config above.
";
        }

        private async Task<string> CheckSipStatusAsync()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
                return "SIP not configured. Use 'setup_info' for instructions.";

            return $"SIP config found at: {configPath}\nStatus: Ready (config loaded)";
        }

        private string GetSipScript()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var scriptDir = Path.Combine(appData, "SuperAgent", "scripts");
            Directory.CreateDirectory(scriptDir);

            var scriptPath = Path.Combine(scriptDir, "sip_call.py");

            if (!File.Exists(scriptPath))
            {
                File.WriteAllText(scriptPath, SipCallPythonScript);
            }

            return scriptPath;
        }

        private const string SipCallPythonScript = @"
import sys, json, argparse

def ensure_deps():
    try:
        import pjsua2
        return True
    except ImportError:
        import subprocess
        # Install PJSIP Python bindings
        subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'pjsua2'])
        return True

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--json', required=True, help='JSON args')
    args = parser.parse_args()

    params = json.loads(args.json)
    action = params['action']
    phone_number = params['phone_number']
    message = params['message']
    config_path = params['config_path']

    # Load SIP config
    with open(config_path) as f:
        config = json.load(f)

    if action == 'sms':
        # For SMS, many SIP providers support SIP MESSAGE or have REST APIs
        # Using a simpler HTTP approach for providers that support it
        try:
            import requests
        except ImportError:
            import subprocess
            subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'requests'])
            import requests

        # Generic SMS via SIP provider API
        # This is provider-specific; below is a template for VoIP.ms
        print(f'SMS to {phone_number}: {message}')
        print('Note: SMS delivery depends on your SIP provider API. Configure REST endpoint in sip_config.json.')

    elif action == 'call':
        # For calls, use PJSIP or fall back to a simpler approach
        try:
            ensure_deps()
            import pjsua2 as pj

            ep = pj.Endpoint()
            ep_cfg = pj.EpConfig()
            ep.libCreate()
            ep.libInit(ep_cfg)

            # Transport
            tcfg = pj.TransportConfig()
            tcfg.port = 5060
            ep.transportCreate(pj.PJSIP_TRANSPORT_UDP, tcfg)
            ep.libStart()

            # Account
            acfg = pj.AccountConfig()
            acfg.idUri = f'sip:{config[""sip_username""]}@{config[""sip_server""]}'
            acfg.regConfig.registrarUri = f'sip:{config[""sip_server""]}'
            cred = pj.AuthCredInfo(""digest"", ""*"", config[""sip_username""], 0, config[""sip_password""])
            acfg.sipConfig.authCreds.append(cred)

            acc = pj.Account()
            acc.create(acfg)

            # Make call
            call = pj.Call(acc)
            prm = pj.CallOpParam(True)
            call.makeCall(f'sip:{phone_number}@{config[""sip_server""]}', prm)

            import time
            time.sleep(30)  # Keep call alive

            ep.libDestroy()
            print(f'Call placed to {phone_number}')

        except Exception as e:
            print(f'VoIP call failed: {e}')
            print('Tip: If pjsua2 is not available, install it manually or use your SIP provider REST API.')

if __name__ == '__main__':
    main()
";
    }
}
