using FSUIPC;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WorkingTitle2GSX
{
    public partial class MainWindow : Window
    {
        protected NotifyIconViewModel notifyModel;
        protected ServiceModel serviceModel;
        protected DispatcherTimer timer;
        protected int lineCounter = 0;

        public MainWindow(NotifyIconViewModel notifyModel, ServiceModel serviceModel)
        {
            InitializeComponent();
            Logger.Log(LogLevel.Verbose, "MainWindow:MainWindow", $"Components Intialized");
            this.notifyModel = notifyModel;
            this.serviceModel = serviceModel;
            string assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.Log(LogLevel.Verbose, "MainWindow:MainWindow", $"assemblyVersion {assemblyVersion}");
            assemblyVersion = assemblyVersion[0..assemblyVersion.LastIndexOf('.')];
            Title += "  (" + assemblyVersion + ")";

            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += OnTick;
            Logger.Log(LogLevel.Verbose, "MainWindow:MainWindow", $"Timer created");
        }

        protected void LoadSettings()
        {
            txtSimBriefID.Text = serviceModel.SimBriefID;
            chkUseActualPaxValue.IsChecked = serviceModel.UseActualPaxValue;
            chkNoCrewBoarding.IsChecked = serviceModel.NoCrewBoarding;
            txtGallonsPerSecond.Text = Convert.ToString(serviceModel.GallonsPerSecond, CultureInfo.CurrentUICulture);
            chkResetFuel.IsChecked = serviceModel.ResetFuel;
            txtStartFuelWingPercent.Text = Convert.ToString(serviceModel.WingTankStartValue, CultureInfo.CurrentUICulture);

            string[] parts = serviceModel.DistributionPax.Split(';');
            double bizPax = Convert.ToDouble(parts[0], new RealInvariantFormat(parts[0]));
            double premPax = Convert.ToDouble(parts[1], new RealInvariantFormat(parts[1]));
            double ecoPax = Convert.ToDouble(parts[2], new RealInvariantFormat(parts[2]));
            txtPaxBusiness.Text = Convert.ToString(bizPax, CultureInfo.CurrentUICulture);
            txtPaxPremium.Text = Convert.ToString(premPax, CultureInfo.CurrentUICulture);
            txtPaxEconomy.Text = Convert.ToString(ecoPax, CultureInfo.CurrentUICulture);

            parts = serviceModel.DistributionCargo.Split(';');
            double cargoFwd = Convert.ToDouble(parts[0], new RealInvariantFormat(parts[0]));
            double cargoAft = Convert.ToDouble(parts[1], new RealInvariantFormat(parts[1]));
            txtCargoForward.Text = Convert.ToString(cargoFwd, CultureInfo.CurrentUICulture);
            txtCargoAft.Text = Convert.ToString(cargoAft, CultureInfo.CurrentUICulture);
        }

        protected void UpdateLogArea()
        {
            while (Logger.MessageQueue.Count > 0)
            {

                if (lineCounter > 6)
                    txtLogMessages.Text = txtLogMessages.Text[(txtLogMessages.Text.IndexOf('\n') + 1)..];
                txtLogMessages.Text += (lineCounter > 0 ? "\n" : "") + Logger.MessageQueue.Dequeue().ToString();
                lineCounter++;
            }
        }

        protected void UpdateStatus()
        {
            if (serviceModel.IsSimRunning)
                lblConnStatMSFS.Foreground = new SolidColorBrush(Colors.DarkGreen);
            else
                lblConnStatMSFS.Foreground = new SolidColorBrush(Colors.Red);

            if (FSUIPCConnection.IsOpen)
                lblConnStatFsuipc.Foreground = new SolidColorBrush(Colors.DarkGreen);
            else
                lblConnStatFsuipc.Foreground = new SolidColorBrush(Colors.Red);

            if (serviceModel.IsWT787Selected)
                lblConnStat787.Foreground = new SolidColorBrush(Colors.DarkGreen);
            else
                lblConnStat787.Foreground = new SolidColorBrush(Colors.Red);

            if (serviceModel.IsSessionRunning)
                lblConnStatSession.Foreground = new SolidColorBrush(Colors.DarkGreen);
            else
                lblConnStatSession.Foreground = new SolidColorBrush(Colors.Red);
        }

        protected void OnTick(object sender, EventArgs e)
        {
            Logger.Log(LogLevel.Verbose, "MainWindow:OnTick", $"Tick started - Update Log Area");
            UpdateLogArea();
            Logger.Log(LogLevel.Verbose, "MainWindow:OnTick", $"Tick started - Update Status");
            UpdateStatus();
            Logger.Log(LogLevel.Verbose, "MainWindow:OnTick", $"Tick executed");
        }

        protected void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible)
            {
                notifyModel.CanExecuteHideWindow = false;
                notifyModel.CanExecuteShowWindow = true;
                timer.Stop();
            }
            else
            {
                Logger.Log(LogLevel.Verbose, "MainWindow:Window_IsVisibleChanged", $"GUI Visible - Loading Settings");
                LoadSettings();
                Logger.Log(LogLevel.Verbose, "MainWindow:Window_IsVisibleChanged", $"GUI Visible - Starting Timer");
                timer.Start();
                Logger.Log(LogLevel.Verbose, "MainWindow:Window_IsVisibleChanged", $"GUI Visible - Timer started");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void txtSimBriefID_Set()
        {
            if (int.TryParse(txtSimBriefID.Text, CultureInfo.InvariantCulture, out int id))
                serviceModel.SetSetting("pilotID", Convert.ToString(id, CultureInfo.InvariantCulture));

            LoadSettings();
        }

        private void txtSimBriefID_LostFocus(object sender, RoutedEventArgs e)
        {
            txtSimBriefID_Set();
        }

        private void txtSimBriefID_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || e.Key != System.Windows.Input.Key.Return)
                return;

            txtSimBriefID_Set();
        }

        private void chkUseActualPaxValue_Click(object sender, RoutedEventArgs e)
        {
            serviceModel.SetSetting("useActualValue", chkUseActualPaxValue.IsChecked.ToString().ToLower());
        }

        private void txtGallonsPerSecond_Set()
        {
            if (double.TryParse(txtGallonsPerSecond.Text, new RealInvariantFormat(txtGallonsPerSecond.Text), out double gps))
                serviceModel.SetSetting("gallonsPerSecond", Convert.ToString(gps, CultureInfo.InvariantCulture));

            LoadSettings();
        }

        private void txtGallonsPerSecond_LostFocus(object sender, RoutedEventArgs e)
        {
            txtGallonsPerSecond_Set();
        }

        private void txtGallonsPerSecond_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || e.Key != System.Windows.Input.Key.Return)
                return;

            txtGallonsPerSecond_Set();
        }

        private void chkNoCrewBoarding_Click(object sender, RoutedEventArgs e)
        {
            serviceModel.SetSetting("noCrewBoarding", chkNoCrewBoarding.IsChecked.ToString().ToLower());
        }

        private void chkResetFuel_Click(object sender, RoutedEventArgs e)
        {
            serviceModel.SetSetting("resetFuel", chkResetFuel.IsChecked.ToString().ToLower());
        }

        private void SetPaxDistribution()
        {
            if (double.TryParse(txtPaxBusiness.Text, new RealInvariantFormat(txtPaxBusiness.Text), out double paxBiz)
                && double.TryParse(txtPaxPremium.Text, new RealInvariantFormat(txtPaxPremium.Text), out double paxPrem)
                && double.TryParse(txtPaxEconomy.Text, new RealInvariantFormat(txtPaxEconomy.Text), out double paxEco))
            {
                serviceModel.SetSetting("distPaxPercent", $"{Convert.ToString(paxBiz, CultureInfo.InvariantCulture)};{Convert.ToString(paxPrem, CultureInfo.InvariantCulture)};{Convert.ToString(paxEco, CultureInfo.InvariantCulture)}");
            }

            LoadSettings();
        }

        private void txtPaxDistribution_LostFocus(object sender, RoutedEventArgs e)
        {
            SetPaxDistribution();
        }

        private void txtPaxDistribution_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || e.Key != System.Windows.Input.Key.Return)
                return;

            SetPaxDistribution();
        }

        private void SetCargoDistribution()
        {
            if (double.TryParse(txtCargoForward.Text, new RealInvariantFormat(txtCargoForward.Text), out double cargoFwd)
                && double.TryParse(txtCargoAft.Text, new RealInvariantFormat(txtCargoAft.Text), out double cargoAft))
            {
                serviceModel.SetSetting("distCargoPercent", $"{Convert.ToString(cargoFwd, CultureInfo.InvariantCulture)};{Convert.ToString(cargoAft, CultureInfo.InvariantCulture)}");
            }

            LoadSettings();
        }

        private void txtCargoDistribution_LostFocus(object sender, RoutedEventArgs e)
        {
            SetCargoDistribution();
        }

        private void txtCargoDistribution_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || e.Key != System.Windows.Input.Key.Return)
                return;

            SetCargoDistribution();
        }

        private void txtStartFuelWingPercent_Set()
        {
            if (double.TryParse(txtStartFuelWingPercent.Text, new RealInvariantFormat(txtStartFuelWingPercent.Text), out double wings))
                serviceModel.SetSetting("startFuelWingPercent", Convert.ToString(wings, CultureInfo.InvariantCulture));

            LoadSettings();
        }

        private void txtStartFuelWingPercent_LostFocus(object sender, RoutedEventArgs e)
        {
            txtStartFuelWingPercent_Set();
        }

        private void txtStartFuelWingPercent_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter || e.Key != System.Windows.Input.Key.Return)
                return;

            txtStartFuelWingPercent_Set();
        }
    }
}
