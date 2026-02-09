using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services.Tools
{
    /// <summary>
    /// Browses the web using a headless Chromium browser via Playwright (Python).
    /// Supports navigation, screenshots, clicking, form-filling, and JavaScript execution.
    /// Falls back to the existing ReadWebPageTool for simple HTML fetching.
    /// </summary>
    public class BrowseWebTool : IAiTool
    {
        private readonly VenvManagerService _venv;

        public string Name => "browse_web";
        public string Description => "Opens a URL in a headless Chromium browser (via Playwright). Supports JavaScript-heavy sites, screenshots, clicking, and form-filling. Use this for interactive web pages. For simple HTML, prefer read_web_page.";
        public bool IsUnsafe => true;

        public object Parameters => new
        {
            type = "object",
            properties = new
            {
                url = new
                {
                    type = "string",
                    description = "URL to navigate to"
                },
                action = new
                {
                    type = "string",
                    description = "Action: 'get_text' (default), 'screenshot', 'click', 'fill', 'evaluate'",
                    @enum = new[] { "get_text", "screenshot", "click", "fill", "evaluate" }
                },
                selector = new
                {
                    type = "string",
                    description = "CSS selector for click/fill actions"
                },
                value = new
                {
                    type = "string",
                    description = "Value for fill action or JavaScript code for evaluate action"
                },
                wait_seconds = new
                {
                    type = "integer",
                    description = "Seconds to wait for page load (default: 5)"
                }
            },
            required = new[] { "url" }
        };

        public BrowseWebTool(VenvManagerService venv)
        {
            _venv = venv;
        }

        public async Task<string> ExecuteAsync(string jsonArgs)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonArgs);
                var root = doc.RootElement;

                var url = "";
                var action = "get_text";
                var selector = "";
                var value = "";
                var waitSeconds = 5;

                if (root.TryGetProperty("url", out var urlProp))
                    url = urlProp.GetString() ?? "";
                if (root.TryGetProperty("action", out var actProp))
                    action = actProp.GetString() ?? "get_text";
                if (root.TryGetProperty("selector", out var selProp))
                    selector = selProp.GetString() ?? "";
                if (root.TryGetProperty("value", out var valProp))
                    value = valProp.GetString() ?? "";
                if (root.TryGetProperty("wait_seconds", out var waitProp))
                    waitSeconds = waitProp.GetInt32();

                if (string.IsNullOrWhiteSpace(url))
                    return "Error: url is required.";

                // Ensure Playwright is installed in the venv
                // (first use will auto-install)
                var scriptPath = GetBrowseScript();

                // Build JSON args for the Python script
                var scriptArgs = JsonSerializer.Serialize(new
                {
                    url,
                    action,
                    selector,
                    value,
                    wait_seconds = waitSeconds
                });

                // Escape for command line
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

        /// <summary>
        /// Returns path to the browse helper script, creating it if needed.
        /// </summary>
        private string GetBrowseScript()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var scriptDir = Path.Combine(appData, "SuperAgent", "scripts");
            Directory.CreateDirectory(scriptDir);

            var scriptPath = Path.Combine(scriptDir, "browse_web.py");

            if (!File.Exists(scriptPath))
            {
                File.WriteAllText(scriptPath, BrowseWebPythonScript);
            }

            return scriptPath;
        }

        private const string BrowseWebPythonScript = @"
import sys, json, argparse

def ensure_playwright():
    try:
        from playwright.sync_api import sync_playwright
        return True
    except ImportError:
        import subprocess
        subprocess.check_call([sys.executable, '-m', 'pip', 'install', 'playwright'])
        subprocess.check_call([sys.executable, '-m', 'playwright', 'install', 'chromium'])
        return True

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--json', required=True, help='JSON args')
    args = parser.parse_args()

    params = json.loads(args.json)
    url = params['url']
    action = params.get('action', 'get_text')
    selector = params.get('selector', '')
    value = params.get('value', '')
    wait_seconds = params.get('wait_seconds', 5)

    ensure_playwright()
    from playwright.sync_api import sync_playwright

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        page.goto(url, wait_until='domcontentloaded', timeout=wait_seconds * 1000)
        page.wait_for_timeout(2000)  # Extra settle time

        result = ''

        if action == 'get_text':
            result = page.inner_text('body')
            # Truncate to avoid massive output
            if len(result) > 8000:
                result = result[:8000] + '\n... [truncated]'

        elif action == 'screenshot':
            import os, tempfile
            screenshot_path = os.path.join(tempfile.gettempdir(), 'super_agent_screenshot.png')
            page.screenshot(path=screenshot_path, full_page=True)
            result = f'Screenshot saved to: {screenshot_path}'

        elif action == 'click':
            if not selector:
                result = 'Error: selector is required for click action'
            else:
                page.click(selector)
                page.wait_for_timeout(1000)
                result = f'Clicked: {selector}. Page title: {page.title()}'

        elif action == 'fill':
            if not selector or not value:
                result = 'Error: selector and value are required for fill action'
            else:
                page.fill(selector, value)
                result = f'Filled {selector} with value'

        elif action == 'evaluate':
            if not value:
                result = 'Error: value (JS code) is required for evaluate action'
            else:
                js_result = page.evaluate(value)
                result = json.dumps(js_result, default=str)

        browser.close()
        print(result)

if __name__ == '__main__':
    main()
";
    }
}
