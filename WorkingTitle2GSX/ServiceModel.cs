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

        private static readonly int BuildConfigVersion = 2;
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
        public string DistributionPaxKU { get; set; }
        public string DistributionCargoKU { get; set; }
        public string DistributionPaxHS { get; set; }
        public string DistributionCargoHS { get; set; }
        public double RearCargoMaxHS { get; set; }
        public double RearCargoMaxKU { get; set; }
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
            DistributionPaxWT = Convert.ToString(ConfigurationFile.GetSetting("distPaxWorkingTitle", "3=0.1105;4=0.1046;5=0.7849"));
            DistributionCargoWT = Convert.ToString(ConfigurationFile.GetSetting("distCargoWorkingTitle", "6=0.70;7=0.30"));
            DistributionPaxKU = Convert.ToString(ConfigurationFile.GetSetting("distPaxKuro", "3=0.163;4=0.114;5=0.723"));
            DistributionCargoKU = Convert.ToString(ConfigurationFile.GetSetting("distCargoKuro", "6=0.60;7=0.40"));
            DistributionPaxHS = Convert.ToString(ConfigurationFile.GetSetting("distPaxHorizon", "3=0.083;4=0.083;5=0.124;6=0.16;7=0.16;8=0.213;9=0.177"));
            DistributionCargoHS = Convert.ToString(ConfigurationFile.GetSetting("distCargoHorizon", "10=-2;11=-1"));
            RearCargoMaxHS = Convert.ToDouble(ConfigurationFile.GetSetting("rearCargoMaxHS", "11924"), new RealInvariantFormat(ConfigurationFile.GetSetting("rearCargoMaxHS", "11924")));
            RearCargoMaxKU = Convert.ToDouble(ConfigurationFile.GetSetting("rearCargoMaxKU", "14168"), new RealInvariantFormat(ConfigurationFile.GetSetting("rearCargoMaxKU", "14168")));
            WingTankStartValue = Convert.ToDouble(ConfigurationFile.GetSetting("startFuelWingPercent", "5"), new RealInvariantFormat(ConfigurationFile.GetSetting("startFuelWingPercent", "5")));
            ResetFuel = Convert.ToBoolean(ConfigurationFile.GetSetting("resetFuel", "true"));

            if (ConfigVersion < BuildConfigVersion)
            {
                //CHANGE SETTINGS IF NEEDED, Example:
                //SetSetting("yada", "true", true);
                SetSetting("distPaxHorizon", "3=0.083;4=0.083;5=0.124;6=0.16;7=0.16;8=0.213;9=0.177", true);
                SetSetting("distPaxWorkingTitle", "3=0.1105;4=0.1046;5=0.7849", true);
                SetSetting("distCargoWorkingTitle", "6=0.70;7=0.30", true);

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