using FSUIPC;
using System;
using System.Threading;

namespace WorkingTitle2GSX
{
    public class ServiceController
    {
        protected ServiceModel Model;
        protected IPCManager IPCManager;
        protected int Interval = 1000;

        public ServiceController(ServiceModel model, IPCManager iPCManager)
        {
            Model = model;
            IPCManager = iPCManager;
        }

        public void Run()
        {
            try
            {
                Logger.Log(LogLevel.Information, "ServiceController:Run", $"Service starting ...");
                while (!Model.CancellationRequested)
                {
                    if (Wait())
                    {
                        ServiceLoop();
                    }
                    else
                    {
                        if (!IPCManager.IsSimRunning())
                        {
                            Model.CancellationRequested = true;
                            Model.ServiceExited = true;
                            App.Current.Shutdown();
                            Logger.Log(LogLevel.Critical, "ServiceController:Run", $"Session aborted, Retry not possible - exiting Program");
                            return;
                        }
                        else
                        {
                            Reset();
                            Logger.Log(LogLevel.Information, "ServiceController:Run", $"Session aborted, Retry possible - Waiting for new Session");
                        }
                    }
                }

                Reset();
                IPCManager.CloseSafe();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Critical, "ServiceController:Run", $"Critical Exception occured: {ex.Source} - {ex.Message}");
                if (!IPCManager.IsSimRunning())
                {
                    Model.CancellationRequested = true;
                    Model.ServiceExited = true;
                    Logger.Log(LogLevel.Critical, "ServiceController:Run", $"Sim not running - exiting Program");
                    return;
                }
            }
        }

        protected bool Wait()
        {
            if (!IPCManager.WaitForSimulator(Model))
                return false;
            else
                Model.IsSimRunning = true;

            if (!IPCManager.WaitForConnection())
                return false;

            if (!IPCManager.WaitForAircraft(Model))
            {
                Model.IsWT787Selected = false;
                return false;
            }
            else
                Model.IsWT787Selected = true;

            if (!IPCManager.WaitForSessionReady(Model))
            {
                Model.IsSessionRunning = false;
                return false;
            }
            else
                Model.IsSessionRunning = true;

            return true;
        }

        protected void Reset()
        {
            try
            {
                Model.IsSessionRunning = false;
                Model.IsWT787Selected = false;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Critical, "ServiceController:Reset", $"Exception during Reset: {ex.Source} - {ex.Message}");
            }
        }

        protected void ServiceLoop()
        {
            Thread.Sleep(1000);
            FlightPlan flightPlan = new(Model);
            Aircraft aircraft = new(Model);

            Offset<short> offsetGround = new(ServiceModel.IpcGroupName, 0x0366);
            int state = 0;
            int sleep = 2000;
            int sleepCounter = 0;
            bool refueling = false;
            bool refuelPaused = false;
            bool refuelFinished = false;
            bool boarding = false;
            bool boardFinished = false;
            bool deboarding = false;
            bool firstStart = true;
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Starting Service Loop - Current State: Pre-Flight (First Leg)");
            while (!Model.CancellationRequested && IPCManager.IsSimRunning() && IPCManager.IsCamReady())
            {
                try
                {
                    if (sleepCounter < sleep)
                    {
                        Thread.Sleep(1000);
                        sleepCounter += 1000;
                        continue;
                    }
                    else
                        sleepCounter = 0;


                    if (firstStart && offsetGround.Value == 1 && !Model.TestArrival && Model.ResetFuel)
                    {
                        aircraft.SetEmpty();
                    }
                    if (firstStart)
                        firstStart = false;

                    //Pre-Flight - First-Flight
                    if (state == 0 && FSUIPCConnection.ReadLVar("XMLVAR_Battery_Switch_State") == 1)
                    {
                        if (!flightPlan.Load())
                        {
                            Logger.Log(LogLevel.Error, "ServiceController:ServiceLoop", "Could not load Flightplan");
                            sleep = 5000;
                            continue;
                        }
                        FSUIPCConnection.WriteLVar("FSDT_GSX_SET_PROGRESS_REFUEL", -1);
                        flightPlan.SetPassengersGSX();
                        aircraft.SetPayload(flightPlan);
                        state = 1;
                        sleep = 1000;

                        if (!Model.TestArrival)
                        {
                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: At Depature Gate");
                        }
                        else
                        {
                            state = 3; // in Flight
                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Test Arrival: Plane is in 'Flight'");
                        }
                        continue;
                    }
                    //Special Case: loaded in Flight
                    if (state == 0 && offsetGround.Value == 0 && IPCManager.IsCamReady() && IPCManager.IsSimRunning())
                    {
                        flightPlan.Load();
                        flightPlan.SetPassengersGSX();
                        aircraft.SetPayload(flightPlan);

                        state = 3;
                        sleep = 180000;
                        Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: Flight");
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
                                    Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Fuel Hose connected - refueling");
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
                                    Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Fuel Hose disconnected - waiting for Truck.");
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
                        Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: Taxi Out");
                        continue;
                    }

                    FSUIPCConnection.Process(ServiceModel.IpcGroupName);
                    if (state <= 3)
                    {
                        //Taxi Out -> Flight
                        if (state <= 2 && offsetGround.Value == 0 && IPCManager.IsCamReady() && IPCManager.IsSimRunning())
                        {
                            state = 3;
                            sleep = 180000;
                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: Flight");
                            continue;
                        }

                        //Flight -> Taxi In
                        if (state == 3 && offsetGround.Value == 1)
                        {
                            state = 4;
                            sleep = 10000;
                            ResetGsxVars();
                            flightPlan.SetPassengersGSX();

                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: Taxi In");
                            continue;
                        }
                    }

                    //Taxi In -> At Arrival Gate
                    if (state == 4 && FSUIPCConnection.ReadLVar("FSDT_VAR_EnginesStopped") == 1)
                    {
                        state = 5;
                        sleep = 3000;

                        Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: At Arrival Gate");
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
                                Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: Turn-Around - Waiting for new OFP");
                                state = 6;
                                continue;
                            }
                        }
                    }

                    //Pre-Flight - Turn-Around
                    if (state == 6)
                    {
                        if (flightPlan.Load())
                        {
                            Logger.Log(LogLevel.Debug, "ServiceController:ServiceLoop", "Resetting GSX Vars");
                            ResetGsxVars();
                            Logger.Log(LogLevel.Debug, "ServiceController:ServiceLoop", "Import SimBrief to GSX");
                            FSUIPCConnection.WriteLVar("FSDT_GSX_MENU_OPEN", 1);
                            Thread.Sleep(2000);
                            FSUIPCConnection.WriteLVar("FSDT_GSX_MENU_CHOICE", 14);
                            Thread.Sleep(2000);
                            flightPlan.SetPassengersGSX();
                            aircraft.SetPayload(flightPlan);
                            state = 1;
                            sleep = 1000;
                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Current State: At Depature Gate");
                            continue;
                        }
                        else
                        {
                            sleep = 60000;
                            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "No new OFP found - Retry in 60s.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Critical, "ServiceController:ServiceLoop", $"Critical Exception during ServiceLoop() {ex.GetType()} {ex.Message} {ex.Source}");
                }
            }

            Model.IsSimRunning = false;
            Model.IsWT787Selected = false;
            Model.IsSessionRunning = false;            
            Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "ServiceLoop ended");
        }

        protected static void ResetGsxVars()
        {
            FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPASSENGERS_BOARDING_TOTAL", 0);
            FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPASSENGERS_DEBOARDING_TOTAL", 0);
            FSUIPCConnection.WriteLVar("FSDT_GSX_BOARDING_CARGO_PERCENT", 0);
            FSUIPCConnection.WriteLVar("FSDT_GSX_DEBOARDING_CARGO_PERCENT", 0);
        }
    }
}
