using System;
using System.Configuration;

namespace WorkingTitle2GSX
{
    public class ServiceModel
    {
        public bool ServiceExited { get; set; } = false;
        public bool CancellationRequested { get; set; } = false;

        public bool IsSimRunning { get; set; } = false;
        public bool IsWT787Selected{ get; set; } = false;
        public bool IsSessionRunning { get; set; } = false;

        public string LogLevel { get; set; }
        public string LogFilePath { get; set; }
        public string SimBriefURL { get; set; }
        public string SimBriefID {  get; set; }
        public bool UseActualPaxValue { get; set; }
        public bool NoCrewBoarding { get; set; }
        public bool TestArrival { get; set; }
        public double GallonsPerSecond { get; set; }
        public double ConstPercent { get; set; }
        public double ConstFuelWeight { get; set; }
        public double ConstKilo { get; set; }
        public double ConstMaxWing { get; set; }
        public double ConstMaxCenter { get; set; }
        public string DistributionPax {  get; set; }
        public string DistributionCargo { get; set; }
        public bool ResetFuel { get; set; }
        public double WingTankStartValue { get; set; }
        public static readonly string IpcGroupName = "WorkingTitle2GSX";
        public string AcIndentified { get; set; }


        protected Configuration AppConfiguration;

        public ServiceModel()
        {
            LoadConfiguration();
        }

        protected void LoadConfiguration()
        {
            AppConfiguration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = AppConfiguration.AppSettings.Settings;

            LogFilePath = Convert.ToString(settings["logFilePath"].Value);
            SimBriefURL = Convert.ToString(settings["simbriefURL"].Value);
            SimBriefID = Convert.ToString(settings["pilotID"].Value);
            UseActualPaxValue = Convert.ToBoolean(settings["useActualValue"].Value);
            NoCrewBoarding = Convert.ToBoolean(settings["noCrewBoarding"].Value);
            TestArrival = Convert.ToBoolean(settings["testArrival"].Value);
            GallonsPerSecond = Convert.ToDouble(settings["gallonsPerSecond"].Value, new RealInvariantFormat(settings["gallonsPerSecond"].Value));
            ConstKilo = Convert.ToDouble(settings["constKilo"].Value, new RealInvariantFormat(settings["constKilo"].Value));
            ConstPercent = Convert.ToDouble(settings["constPercent"].Value, new RealInvariantFormat(settings["constPercent"].Value));
            DistributionPax = Convert.ToString(settings["distPaxPercent"].Value);
            DistributionCargo = Convert.ToString(settings["distCargoPercent"].Value);
            WingTankStartValue = Convert.ToDouble(settings["startFuelWingPercent"].Value, new RealInvariantFormat(settings["startFuelWingPercent"].Value));
            ResetFuel = Convert.ToBoolean(settings["resetFuel"].Value);
        }

        protected void SaveConfiguration()
        {
            AppConfiguration.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection(AppConfiguration.AppSettings.SectionInformation.Name);
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            return AppConfiguration.AppSettings.Settings[key].Value ?? defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            if (AppConfiguration.AppSettings.Settings[key] != null)
            {
                AppConfiguration.AppSettings.Settings[key].Value = value;
                SaveConfiguration();
                LoadConfiguration();
            }
        }
    }
}