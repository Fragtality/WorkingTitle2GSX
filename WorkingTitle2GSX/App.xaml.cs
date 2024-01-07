using H.NotifyIcon;
using Serilog;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WorkingTitle2GSX
{
    public partial class App : Application
    {
        private ServiceModel Model;
        private IPCManager IPCManager;
        private ServiceController Controller;

        private TaskbarIcon notifyIcon;

        public static new App Current => Application.Current as App;
        public static string ConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WorkingTitle2GSX\WorkingTitle2GSX.config";
        public static string AppDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WorkingTitle2GSX\bin";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (Process.GetProcessesByName("WorkingTitle2GSX").Length > 1)
            {
                MessageBox.Show("WorkingTitle2GSX is already running!", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            Directory.SetCurrentDirectory(AppDir);

            if (!File.Exists(ConfigFile))
            {
                ConfigFile = Directory.GetCurrentDirectory() + @"\WorkingTitle2GSX.config";
                if (!File.Exists(ConfigFile))
                {
                    MessageBox.Show("No Configuration File found! Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }
            }

            Model = new();
            InitLog();
            InitSystray();

            Logger.Log(LogLevel.Information, "App:InitSystray", $"Creating IPC Manager ...");
            IPCManager = new();
            Controller = new(Model, IPCManager);
            Task.Run(Controller.Run);

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += OnTick;
            timer.Start();

            MainWindow = new MainWindow(notifyIcon.DataContext as NotifyIconViewModel, Model);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Model.CancellationRequested = true;
            notifyIcon?.Dispose();
            base.OnExit(e);

            Logger.Log(LogLevel.Information, "App:OnExit", "WorkingTitle2GSX exiting ...");
        }

        protected void OnTick(object sender, EventArgs e)
        {
            if (Model.ServiceExited)
            {
                Current.Shutdown();
            }
        }

        protected void InitLog()
        {
            string logFilePath = @"..\log\" + Model.GetSetting("logFilePath", "WorkingTitle2GSX.log");
            string logLevel = Model.GetSetting("logLevel", "Debug");
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration().WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3,
                                                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message} {NewLine}{Exception}");
            if (logLevel == "Warning")
                loggerConfiguration.MinimumLevel.Warning();
            else if (logLevel == "Debug")
                loggerConfiguration.MinimumLevel.Debug();
            else if (logLevel == "Verbose")
                loggerConfiguration.MinimumLevel.Verbose();
            else
                loggerConfiguration.MinimumLevel.Information();
            Log.Logger = loggerConfiguration.CreateLogger();
            Log.Information($"-----------------------------------------------------------------------");
            Logger.Log(LogLevel.Information, "App:InitLog", $"WorkingTitle2GSX started! Log Level: {logLevel} Log File: {logFilePath}");
        }

        protected void InitSystray()
        {
            Logger.Log(LogLevel.Information, "App:InitSystray", $"Creating SysTray Icon ...");
            notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            notifyIcon.Icon = GetIcon("logo.ico");
            notifyIcon.ForceCreate(false);
        }

        public Icon GetIcon(string filename)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"WorkingTitle2GSX.{filename}");
            return new Icon(stream);
        }
    }
}
