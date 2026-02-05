using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Windows.Services;
using OpenClaw.Windows.Services.Data;
using OpenClaw.Windows.Services.Tools;
using OpenClaw.Windows.Views;

namespace OpenClaw.Windows
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window window = Window.Current;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public IHost Host { get; }

        public static new App Current => (App)Application.Current;

        public App()
        {
            this.InitializeComponent();

            Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<Services.Data.ChatContextDb>();
                    services.AddSingleton<Services.OnnxLocalAiService>();
                    services.AddSingleton<Services.GoogleGeminiService>();
                    services.AddSingleton<ISlackService, SlackService>();
                    services.AddSingleton<Services.SafetyService>();
                    services.AddSingleton<Services.AgentService>();
                    
                    // Tools
                    services.AddSingleton<Services.Tools.ToolRegistry>();
                    services.AddSingleton<Services.AgentOrchestrator>();
                    services.AddSingleton<IAiTool, Services.Tools.GetSystemTimeTool>();
                    services.AddSingleton<IAiTool, Services.Tools.ReadFileTool>();
                    services.AddSingleton<IAiTool, Services.Tools.WriteFileTool>();
                    services.AddSingleton<IAiTool, Services.Tools.PowerShellTool>();
                    
                    services.AddSingleton<Services.IAiService, Services.HybridAiService>();
                })
                .Build();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();
            window.Title = "OpenGemini";

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
            
            // Resize Window to be smaller (approx 50%)
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new global::Windows.Graphics.SizeInt32(500, 700));
            
            window.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
