using FSUIPC;
using Serilog;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace WorkingTitle2GSX
{
    public static class Program
    {
        public static string logFilePath = Convert.ToString(ConfigurationManager.AppSettings["logFilePath"]);
        public static string simbriefURL = Convert.ToString(ConfigurationManager.AppSettings["simbriefURL"]);
        public static string pilotID = Convert.ToString(ConfigurationManager.AppSettings["pilotID"]);
        public static bool useActualValue = Convert.ToBoolean(ConfigurationManager.AppSettings["useActualValue"]);
        public static bool noCrewBoarding = Convert.ToBoolean(ConfigurationManager.AppSettings["noCrewBoarding"]);
        public static bool testArrival = Convert.ToBoolean(ConfigurationManager.AppSettings["testArrival"]);
        public static double constGPS = Convert.ToDouble(ConfigurationManager.AppSettings["gallonsPerSecond"], new RealInvariantFormat(ConfigurationManager.AppSettings["gallonsPerSecond"]));
        public static double constFuelWeight;
        public static double constKilo = Convert.ToDouble(ConfigurationManager.AppSettings["constKilo"], new RealInvariantFormat(ConfigurationManager.AppSettings["constKilo"]));
        public static double constPercent = Convert.ToDouble(ConfigurationManager.AppSettings["constPercent"], new RealInvariantFormat(ConfigurationManager.AppSettings["constPercent"]));
        public static double constMaxWing;
        public static double constMaxCenter;
        public static string constPaxDistPercent = Convert.ToString(ConfigurationManager.AppSettings["distPaxPercent"]);
        public static string constCargoDistPercent = Convert.ToString(ConfigurationManager.AppSettings["distCargoPercent"]);
        public static double startFuelWingPercent = Convert.ToDouble(ConfigurationManager.AppSettings["startFuelWingPercent"], new RealInvariantFormat(ConfigurationManager.AppSettings["startFuelWingPercent"]));

        public static string groupName = "WorkingTitle2GSX";
        public static string acIdentified = "";
        public static bool firstStart = true;

        public static bool OpenSafeFSUIPC()
        {
            try
            {
                if (!FSUIPCConnection.IsOpen)
                    FSUIPCConnection.Open();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            return FSUIPCConnection.IsOpen;
        }

        public static string CheckAircraft()
        {
            if (acIdentified.Length > 0)
                return acIdentified;

            Log.Logger.Information("Read Current Aircraft");


            if (OpenSafeFSUIPC())
            {
                Offset airOffset = new Offset(groupName, 0x3C00, 256);
                FSUIPCConnection.Process(groupName);
                string airString = airOffset.GetValue<string>();
                airOffset.Disconnect();

                if (airString != null && airString.Length > 0)
                {
                    if (!airString.Contains("787"))
                    {
                        Log.Logger.Warning("Current Aircraft is not a 787!");
                        return "";
                    }
                    else
                    {
                        if (airString.Contains("787_8"))
                            acIdentified = "B787-8";
                        else if (airString.Contains("787_9"))
                            acIdentified = "B787-9";
                        else if (airString.Contains("787_10"))
                            acIdentified = "B787-10";
                        else
                            Log.Logger.Error($"Library: AirString could not be matched: {airString}");

                        Log.Logger.Debug($"Library: Getting Fuel Information for {acIdentified}");
                        Offset tankCapacityCenter = new Offset(groupName, 0x0B78, 4);
                        Offset tankCapacityWing = new Offset(groupName, 0x0B80, 4);
                        Offset offWeightConversion = new Offset(groupName, 0x0AF4, 2);
                        FSUIPCConnection.Process(groupName);
                        constFuelWeight = offWeightConversion.GetValue<short>() * 0.00390625;
                        constMaxCenter = tankCapacityCenter.GetValue<int>();
                        constMaxWing = tankCapacityWing.GetValue<int>();
                    }
                }
                else
                {
                    Log.Logger.Error("Library: Could not read AIR File from 0x3C00!");
                    return "";
                } 
            }
            else
            {
                Log.Logger.Error("Library: FSUIPC Connection failed!");
                return "";
            }

            Log.Logger.Information($"WorkingTitle {acIdentified} loaded!");
            return acIdentified;
        }

        public static void Main()
        {
            if (File.Exists(logFilePath))
                File.WriteAllText(logFilePath, "");
            Log.Logger = new LoggerConfiguration().WriteTo.File(logFilePath).MinimumLevel.Debug().CreateLogger();
            Log.Information("WorkingTitle2GSX started!");

            while (Process.GetProcessesByName("FlightSimulator").Length > 0 && !OpenSafeFSUIPC())
            {
                Thread.Sleep(5000);
                Log.Logger.Information("Retrying Connection ...");
            }

            if (CheckAircraft() == "")
                return;

            FlightPlan flightPlan = new FlightPlan();
            Aircraft aircraft = new Aircraft();
            
            Offset<short> offsetGround = new Offset<short>(groupName, 0x0366);
            int state = 0;
            int sleep = 3000;
            bool refueling = false;
            bool refuelPaused = false;
            bool refuelFinished = false;
            bool boarding = false;
            bool boardFinished = false;
            bool deboarding = false;
            Log.Logger.Information("Current State: Pre-Flight (First Leg)");
            FSUIPCConnection.Process(groupName);
            while (Process.GetProcessesByName("FlightSimulator").Length > 0)
            {
                Thread.Sleep(sleep);
                if (firstStart && offsetGround.Value == 1 && !testArrival)
                {
                    aircraft.SetEmpty();
                }
                if (firstStart)
                    firstStart = false;
                
                //Pre-Flight - First-Flight
                if (state == 0 && FSUIPCConnection.ReadLVar("XMLVAR_Battery_Switch_State") == 1)
                {
                    flightPlan.Load();
                    flightPlan.SetPassengersGSX();
                    aircraft.SetPayload(flightPlan);
                    state = 1;
                    sleep = 1000;
                    
                    if (!testArrival)
                    {
                        Log.Logger.Information("Current State: At Depature Gate");
                        
                    }
                    else
                    {
                        state = 3; // in Flight
                        Log.Logger.Information("Test Arrival: Plane is in 'Flight'");
                    }
                    continue;
                }
                //Special Case: loaded in Flight
                if (state == 0 && offsetGround.Value == 0)
                {
                    flightPlan.Load();
                    flightPlan.SetPassengersGSX();
                    aircraft.SetPayload(flightPlan);

                    state = 3;
                    sleep = 180000;
                    Log.Logger.Information("Current State: Flight");
                    continue;
                }

                //At Depature Gate
                if (state == 1)
                {
                    if (!refueling && !refuelFinished && FSUIPCConnection.ReadLVar("FSDT_GSX_REFUELING_STATE") == 5)
                    {
                        refueling = true;
                        refuelPaused = true;
                        aircraft.StartRefuel();
                        continue;
                    }
                    else if (refueling)
                    {
                        if (FSUIPCConnection.ReadLVar("FSDT_GSX_FUELHOSE_CONNECTED") == 1)
                        {
                            if (refuelPaused)
                            {
                                Log.Logger.Information("Fuel Hose connected - refueling");
                                refuelPaused = false;
                            }

                            if (aircraft.RefuelAircraft())
                            {
                                refueling = false;
                                refuelFinished = true;
                                refuelPaused = false;
                                aircraft.StopRefuel();
                                sleep = 3000;
                                if (!boarding)
                                    continue;
                            }
                        }
                        else
                        {
                            if (!refuelPaused && !refuelFinished)
                            {
                                Log.Logger.Information("Fuel Hose disconnected - waiting for Truck.");
                                refuelPaused = true;
                            }
                        }
                    }

                    if (!boarding && !boardFinished && (int)FSUIPCConnection.ReadLVar("FSDT_GSX_BOARDING_STATE") >= 4)
                    {
                        boarding = true;
                        aircraft.StartBoarding();
                        continue;
                    }
                    else if (boarding)
                    {
                        if (aircraft.BoardAircraft())
                        {
                            boarding = false;
                            boardFinished = true;
                            aircraft.StopBoarding();
                        }
                    }
                }

                //At Depature -> Taxi Out
                if (state == 1 && refuelFinished && boardFinished)
                {
                    refuelFinished = false;
                    boardFinished = false;
                    state = 2;
                    sleep = 60000;
                    Log.Logger.Information("Current State: Taxi Out");
                    continue;
                }

                FSUIPCConnection.Process(groupName);
                if (state <= 3)
                {
                    //Taxi Out -> Flight
                    if (state <= 2 && offsetGround.Value == 0)
                    {
                        state = 3;
                        sleep = 180000;
                        Log.Logger.Information("Current State: Flight");
                        continue;
                    }

                    //Flight -> Taxi In
                    if (state == 3 && offsetGround.Value == 1)
                    {
                        state = 4;
                        sleep = 10000;
                        flightPlan.SetPassengersGSX();

                        Log.Logger.Information("Current State: Taxi In");
                        continue;
                    }
                }

                //Taxi In -> At Arrival Gate
                if (state == 4 && FSUIPCConnection.ReadLVar("FSDT_VAR_EnginesStopped") == 1)
                {
                    state = 5;
                    sleep = 3000;

                    Log.Logger.Information("Current State: At Arrival Gate");
                    continue;
                }

                //At Arrival Gate
                int deboard_state = (int)FSUIPCConnection.ReadLVar("FSDT_GSX_DEBOARDING_STATE");
                if (state == 5 && deboard_state >= 4)
                {
                    if (!deboarding)
                    {
                        deboarding = true;
                        aircraft.StartDeboarding();
                        sleep = 3000;
                        continue;
                    }
                    else if (deboarding)
                    {
                        if (aircraft.DeboardAircraft() || deboard_state == 6)
                        {
                            deboarding = false;
                            aircraft.StopDeboarding();
                            Log.Logger.Information("Current State: Turn-Around - Check for new OFP in 5 Minutes.");
                            state = 6;
                            Thread.Sleep(240000);
                            sleep = 60000;
                            continue;
                        }
                    }
                }

                //Pre-Flight - Turn-Around
                if (state == 6)
                {
                    if (flightPlan.Load())
                    {
                        flightPlan.SetPassengersGSX();
                        aircraft.SetPayload(flightPlan);
                        state = 1;
                        sleep = 1000;
                        Log.Logger.Information("Current State: At Depature Gate");
                        continue;
                    }
                    else
                        Log.Logger.Information("No new OFP found - Retry in 60s.");
                }
            }


            if (FSUIPCConnection.IsOpen)
                FSUIPCConnection.Close();
        }
    }

    public class RealInvariantFormat : IFormatProvider
    {
        public NumberFormatInfo formatInfo = CultureInfo.InvariantCulture.NumberFormat;

        public RealInvariantFormat(string value)
        {
            if (value == null)
            {
                formatInfo = new CultureInfo("en-US").NumberFormat;
                return;
            }

            int lastPoint = value.LastIndexOf('.');
            int lastComma = value.LastIndexOf(',');
            if (lastComma > lastPoint)
            {
                formatInfo = new CultureInfo("de-DE").NumberFormat;
            }
            else
            {
                formatInfo = new CultureInfo("en-US").NumberFormat;
            }
        }

        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(NumberFormatInfo))
            {
                return formatInfo;
            }
            else
                return null;
        }
    }
}
