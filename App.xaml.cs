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
using System.Threading.Tasks;

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
                    
                    // Autonomous Agent Tools (Phase A-F)
                    services.AddSingleton<Services.VenvManagerService>(); // Shared venv for Python tools
                    services.AddSingleton<IAiTool, Services.Tools.RunPythonTool>();
                    services.AddSingleton<IAiTool, Services.Tools.PipInstallTool>();
                    services.AddSingleton<IAiTool, Services.Tools.BrowseWebTool>();
                    services.AddSingleton<IAiTool, Services.Tools.VoipCallTool>();
                    services.AddSingleton<IAiTool, Services.Tools.SendMessageTool>();
                    services.AddSingleton<IAiTool, Services.Tools.CreateSkillTool>();

                    services.AddSingleton<Services.EmbeddingService>();
                    services.AddSingleton<Services.MemoryService>();
                    services.AddSingleton<Services.OcrService>(); // Local OCR
                    services.AddSingleton<Services.AudioTranscriberService>(); // Local Whisper
                    services.AddSingleton<Services.ToastService>(); // Notifications
                    services.AddSingleton<Services.FileWatcherService>(); // File Sensor
                    services.AddSingleton<Services.Skills.SkillService>(); // Skill System
                    services.AddSingleton<Services.FirebaseAnalyticsService>(); // Firebase Analytics

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
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "startup_debug.log");
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: OnLaunched Started\n");

            window ??= new Window();
            window.Title = "Super Agent 🦸‍♂️";

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Window Created\n");

            // Initialize Skills
            var skillService = Host.Services.GetRequiredService<Services.Skills.SkillService>();
            var toolRegistry = Host.Services.GetRequiredService<Services.Tools.ToolRegistry>();
            
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Services Retrieved\n");

            // Fire and forget, but ideally we wait. Since it's local I/O it's fast.
            _ = Task.Run(async () => 
            {
                try 
                {
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Bg Task Start\n");
                    await skillService.RefreshSkillsAsync();
                    foreach (var skill in skillService.GetSkills())
                    {
                        toolRegistry.RegisterTool(new Services.Skills.SkillAdapter(skill));
                    }
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Bg Task End\n");
                }
                catch(Exception ex)
                {
                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Bg Task Error: {ex}\n");
                }
            });

            _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
            
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Navigated\n");

            // Resize Window to be smaller (approx 50%)
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new global::Windows.Graphics.SizeInt32(780, 700));
            
            // Set Window Icon (Manual set required for unpackaged apps)
            try {
                appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico"));
            } catch {}
            
            window.Activate();
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Window Activated\n");

            // Initialize System Tray
            InitializeSystemTray();
            
            // Handle Closing (Minimize to Tray)
            appWindow.Closing += AppWindow_Closing;

            // Initialize Analytics
            var analytics = Host.Services.GetRequiredService<Services.FirebaseAnalyticsService>();
            _ = analytics.LogEventAsync("app_start");

            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Startup Complete\n");

            // Check EULA
            await CheckEulaAsync();
        }

        private async Task CheckEulaAsync()
        {
            bool isAccepted = Services.SettingsHelper.Get<bool>("EulaAccepted", false);
            
            try
            {
                var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: Start CheckEulaAsync. Value: {isAccepted}\n");
            }
            catch {}

            if (!isAccepted)
            {
                try
                {
                    var root = window.Content as Microsoft.UI.Xaml.FrameworkElement;
                    if (root == null) return;

                    // Wait for XamlRoot
                    if (root.XamlRoot == null)
                    {
                        var tcs = new TaskCompletionSource<object?>();
                        void OnLoaded(object s, Microsoft.UI.Xaml.RoutedEventArgs e) 
                        {
                            root.Loaded -= OnLoaded;
                            tcs.TrySetResult(null);
                        }
                        root.Loaded += OnLoaded;

                        if (root.XamlRoot != null) 
                        {
                            root.Loaded -= OnLoaded;
                        }
                        else
                        {
                             await Task.WhenAny(tcs.Task, Task.Delay(2000));
                        }
                    }

                    if (root.XamlRoot == null)
                    {
                         try
                         {
                            var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                            System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: ERROR - XamlRoot is still null. Cannot show EULA.\n");
                         }
                         catch {}
                         return; // Avoid crash
                    }

                    var dialog = new EulaDialog();
                    dialog.XamlRoot = root.XamlRoot;
                    
                    var result = await dialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        Services.SettingsHelper.Set("EulaAccepted", true);
                    }
                    else
                    {
                        ExitApp();
                    }
                }
                catch (Exception ex)
                {
                     try
                     {
                        var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "settings_debug.log");
                        System.IO.File.AppendAllText(logPath, $"{DateTime.Now}: EULA Dialog Error: {ex}\n");
                     }
                     catch {}
                }
            }
        }

        private H.NotifyIcon.TaskbarIcon? _trayIcon;

        private void InitializeSystemTray()
        {
            _trayIcon = new H.NotifyIcon.TaskbarIcon
            {
                // Use the new Super Agent logo
                IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.png")),
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

        private bool _isExitTriggered = false;

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (_isExitTriggered) return; // Allow close if Exit was clicked

            // Cancel Close and Hide instead
            // args.Cancel = true;
            // sender.Hide();
            ExitApp();
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
            _isExitTriggered = true; // Signal that we really want to exit
            
            // Remove the Tray Icon
            _trayIcon?.Dispose();
            _trayIcon = null;

            // Force Application Exit
            // Application.Current.Exit() can sometimes be blocked by background threads or message loops in WinUI 3.
            // Environment.Exit(0) ensures the process terminates immediately.
            Environment.Exit(0);
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
