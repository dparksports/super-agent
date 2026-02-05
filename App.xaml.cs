using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Windows.Services;
using OpenClaw.Windows.Services.Data;
using OpenClaw.Windows.Services.Tools;
using OpenClaw.Windows.Views;
using H.NotifyIcon;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input; 
using System;

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
                    services.AddSingleton<IAiTool, Services.Tools.WebSearchTool>();
                    services.AddSingleton<IAiTool, Services.Tools.ReadWebPageTool>();
                    services.AddSingleton<IAiTool, Services.Tools.ReadTextFromImageTool>();
                    services.AddSingleton<IAiTool, Services.Tools.TranscribeAudioTool>();
                    
                    services.AddSingleton<Services.EmbeddingService>();
                    services.AddSingleton<Services.MemoryService>();
                    services.AddSingleton<Services.OcrService>(); // Local OCR
                    services.AddSingleton<Services.AudioTranscriberService>(); // Local Whisper
                    services.AddSingleton<Services.ToastService>(); // Notifications
                    services.AddSingleton<Services.FileWatcherService>(); // File Sensor

                    services.AddHostedService<Services.CoreAgentBackgroundService>(); // Background Heartbeat

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
            appWindow.Resize(new global::Windows.Graphics.SizeInt32(600, 700));
            
            window.Activate();

            // Initialize System Tray
            InitializeSystemTray();
            
            // Handle Closing (Minimize to Tray)
            appWindow.Closing += AppWindow_Closing;
        }

        private H.NotifyIcon.TaskbarIcon? _trayIcon;

        private void InitializeSystemTray()
        {
            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                // Use a more standard asset
                IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.scale-200.png")),
                ToolTipText = "Super Agent 🦸‍♂️",
            };
            _trayIcon.LeftClickCommand = new StandardUICommand(StandardUICommandKind.None) { Command = new RelayCommand(() => ShowWindow()) };

            var flyout = new MenuFlyout();
            var openItem = new MenuFlyoutItem { Text = "Open Super Agent" };
            openItem.Click += (s, e) => ShowWindow();
            
            var exitItem = new MenuFlyoutItem { Text = "Exit" };
            exitItem.Click += (s, e) => ExitApp();

            flyout.Items.Add(openItem);
            flyout.Items.Add(new MenuFlyoutSeparator());
            flyout.Items.Add(exitItem);

            _trayIcon.ContextFlyout = flyout;
            _trayIcon.ForceCreate();
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // Cancel Close and Hide instead
            args.Cancel = true;
            sender.Hide();
        }

        private void ShowWindow()
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Show();
            window.Activate();
        }

        private void ExitApp()
        {
            // Remove the Tray Icon
            _trayIcon?.Dispose();
            _trayIcon = null;

            // Force Application Exit
            Application.Current.Exit();
        }

        // Simple RelayCommand helper
        public class RelayCommand : System.Windows.Input.ICommand
        {
            private readonly Action _execute;
            public RelayCommand(Action execute) => _execute = execute;
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute();
#pragma warning disable CS0067
            public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
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
