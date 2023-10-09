using H.NotifyIcon;
using Serilog;
using System;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = Environment.GetCommandLineArgs();
            if (args.Length == 3 && args[1].ToLower() == "-path" && Directory.Exists(args[2]))
                Directory.SetCurrentDirectory(args[2]);

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
            string logFilePath = Model.GetSetting("logFilePath", "WorkingTitle2GSX.log");
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
