using System;

namespace WorkingTitle2GSX
{
    public class ServiceModel
    {
        public bool ServiceExited { get; set; } = false;
        public bool CancellationRequested { get; set; } = false;

        public bool IsSimRunning { get; set; } = false;
        public bool IsWT787Selected{ get; set; } = false;
        public bool IsSessionRunning { get; set; } = false;

        private static readonly int BuildConfigVersion = 1;
        public int ConfigVersion { get; set; }
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
        public string DistributionPaxWT {  get; set; }
        public string DistributionCargoWT { get; set; }
        public string DistributionPaxHS { get; set; }
        public string DistributionCargoHS { get; set; }
        public bool ResetFuel { get; set; }
        public double WingTankStartValue { get; set; }
        public static readonly string IpcGroupName = "WorkingTitle2GSX";
        public string AcIndentified { get; set; }


        protected ConfigurationFile ConfigurationFile = new();

        public ServiceModel()
        {
            LoadConfiguration();
        }

        protected void LoadConfiguration()
        {
            ConfigurationFile.LoadConfiguration();

            ConfigVersion = Convert.ToInt32(ConfigurationFile.GetSetting("ConfigVersion", "1"));
            LogFilePath = Convert.ToString(ConfigurationFile.GetSetting("logFilePath", "WorkingTitle2GSX.log"));
            SimBriefURL = Convert.ToString(ConfigurationFile.GetSetting("simbriefURL", "https://www.simbrief.com/api/xml.fetcher.php?userid={0}"));
            SimBriefID = Convert.ToString(ConfigurationFile.GetSetting("pilotID", "0"));
            UseActualPaxValue = Convert.ToBoolean(ConfigurationFile.GetSetting("useActualValue", "true"));
            NoCrewBoarding = Convert.ToBoolean(ConfigurationFile.GetSetting("noCrewBoarding", "true"));
            TestArrival = Convert.ToBoolean(ConfigurationFile.GetSetting("testArrival", "false"));
            GallonsPerSecond = Convert.ToDouble(ConfigurationFile.GetSetting("gallonsPerSecond", "16,5"), new RealInvariantFormat(ConfigurationFile.GetSetting("gallonsPerSecond", "16,5")));
            ConstKilo = Convert.ToDouble(ConfigurationFile.GetSetting("constKilo", "0,45359237"), new RealInvariantFormat(ConfigurationFile.GetSetting("constKilo", "0,45359237")));
            ConstPercent = Convert.ToDouble(ConfigurationFile.GetSetting("constPercent", "8388608"), new RealInvariantFormat(ConfigurationFile.GetSetting("constPercent", "8388608")));
            DistributionPaxWT = Convert.ToString(ConfigurationFile.GetSetting("distPaxWorkingTitle", "3=0.11;4=0.10;5=0.79"));
            DistributionCargoWT = Convert.ToString(ConfigurationFile.GetSetting("distCargoWorkingTitle", "6=0.40;7=0.60"));
            DistributionPaxHS = Convert.ToString(ConfigurationFile.GetSetting("distPaxHorizon", "3=0.08;4=0.08;5=0.13;6=0.16;7=0.16;8=0.21;9=0.18"));
            DistributionCargoHS = Convert.ToString(ConfigurationFile.GetSetting("distCargoHorizon", "10=-2;11=-1"));
            WingTankStartValue = Convert.ToDouble(ConfigurationFile.GetSetting("startFuelWingPercent", "5"), new RealInvariantFormat(ConfigurationFile.GetSetting("startFuelWingPercent", "5")));
            ResetFuel = Convert.ToBoolean(ConfigurationFile.GetSetting("resetFuel", "true"));

            if (ConfigVersion < BuildConfigVersion)
            {
                //CHANGE SETTINGS IF NEEDED, Example:
                //SetSetting("yada", "true", true);

                SetSetting("ConfigVersion", Convert.ToString(BuildConfigVersion));
            }
        }
        public string GetSetting(string key, string defaultValue = "")
        {
            return ConfigurationFile[key] ?? defaultValue;
        }

        public void SetSetting(string key, string value, bool noLoad = false)
        {
            ConfigurationFile[key] = value;
            if (!noLoad)
                LoadConfiguration();
        }
    }
}