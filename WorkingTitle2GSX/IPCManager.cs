using FSUIPC;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WorkingTitle2GSX
{
    public class IPCManager
    {
        public static readonly int waitDuration = 30000;

        private readonly Offset<byte> offInMenu = new(ServiceModel.IpcGroupName, 0x062B);
        private readonly Offset<byte> offCamReady = new(ServiceModel.IpcGroupName, 0x026D);
        private readonly Offset<string> offAircraft = new(ServiceModel.IpcGroupName, 0x3C00, 256);

        public static bool WaitForSimulator(ServiceModel model)
        {
            bool simRunning = IsSimRunning();
            if (!simRunning)
            {
                do
                {
                    Logger.Log(LogLevel.Information, "IPCManager:WaitForSimulator", $"Simulator not started - waiting {waitDuration / 1000}s for Sim");
                    Thread.Sleep(waitDuration);
                }
                while (!IsSimRunning() && !model.CancellationRequested);

                Thread.Sleep(waitDuration);
                return true;
            }
            else if (simRunning)
            {
                Logger.Log(LogLevel.Information, "IPCManager:WaitForSimulator", $"Simulator started");
                return true;
            }
            else
            {
                Logger.Log(LogLevel.Error, "IPCManager:WaitForSimulator", $"Simulator not started - aborting");
                return false;
            }
        }

        public static bool IsProcessRunning(string name)
        {
            Process proc = Process.GetProcessesByName(name).FirstOrDefault();
            return proc != null && proc.ProcessName == name;
        }

        public static bool IsSimRunning()
        {
            return IsProcessRunning("FlightSimulator");
        }
        
        public bool WaitForConnection()
        {
            if (!IsSimRunning())
                return false;

            try
            {
                if (!FSUIPCConnection.IsOpen)
                    FSUIPCConnection.Open();

                if (FSUIPCConnection.IsOpen)
                {
                    offInMenu.Reconnect();
                    offCamReady.Reconnect();
                    offAircraft.Reconnect();
                    FSUIPCConnection.Process(ServiceModel.IpcGroupName);
                    Logger.Log(LogLevel.Information, "IPCManager:WaitForConnection", $"FSUIPC Connected and Process Call succeeded.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Critical, "IPCManager:WaitForConnection", $"Exception while opening FSUIPC! (Exception: {ex.GetType()})");
            }

            if (!FSUIPCConnection.IsOpen && IsSimRunning())
            {
                Logger.Log(LogLevel.Information, "IPCManager:WaitForConnection", $"FSUIPC Connection not established - waiting {waitDuration / 2 / 1000}s");
                Thread.Sleep(waitDuration / 2);
            }

            return FSUIPCConnection.IsOpen && offInMenu.IsConnected && IsSimRunning();
        }

        public string CheckAircraft(ServiceModel model)
        {
            Logger.Log(LogLevel.Debug, "IPCManager:CheckAircraft", $"Read Current Aircraft");

            if (FSUIPCConnection.IsOpen)
            {
                FSUIPCConnection.Process(ServiceModel.IpcGroupName);
                string airString = offAircraft.GetValue<string>();

                if (!string.IsNullOrEmpty(airString))
                {
                    if (!airString.Contains("787"))
                    {
                        Logger.Log(LogLevel.Debug, "IPCManager:CheckAircraft", $"Current Aircraft is not a 787: {airString}");
                        model.AcIndentified = "";
                    }
                    else
                    {
                        model.AcIndentified = airString;
                    }
                }
                else
                {
                    model.AcIndentified = "";
                    Logger.Log(LogLevel.Debug, "IPCManager:CheckAircraft", $"Could not read AIR File from 0x3C00!");
                }
            }
            else
            {
                model.AcIndentified = "";
                Logger.Log(LogLevel.Error, "IPCManager:CheckAircraft", $"FSUIPC Connection not open!");
            }

            return model.AcIndentified;
        }

        public bool WaitForAircraft(ServiceModel model)
        {
            if (!IsSimRunning())
                return false;

            CheckAircraft(model);
            if (string.IsNullOrEmpty(model.AcIndentified))
            {
                do
                {
                    Logger.Log(LogLevel.Information, "IPCManager:WaitForAircraft", $"No 787 Aircraft selected - waiting {waitDuration / 2 / 1000}s for Retry");
                    Thread.Sleep(waitDuration / 2);

                    CheckAircraft(model);
                }
                while (string.IsNullOrEmpty(model.AcIndentified) && IsSimRunning() && !model.CancellationRequested);

                return !string.IsNullOrEmpty(model.AcIndentified) && IsSimRunning();
            }
            else
            {
                Logger.Log(LogLevel.Information, "IPCManager:WaitForAircraft", $"787 Aircraft selected: {model.AcIndentified}");
                return true;
            }
        }
        
        public bool WaitForSessionReady(ServiceModel model)
        {
            int waitDuration = 5000;
            Thread.Sleep(250);
            bool isReady = IsCamReady();
            while (IsSimRunning() && !isReady && !model.CancellationRequested)
            {
                Logger.Log(LogLevel.Information, "IPCManager:WaitForSessionReady", $"Session not ready - waiting {waitDuration / 1000}s for Retry");
                Thread.Sleep(waitDuration);
                isReady = IsCamReady();
            }

            if (!isReady || !IsSimRunning())
            {
                Logger.Log(LogLevel.Error, "IPCManager:WaitForSessionReady", $"SimConnect or Simulator not available - aborting");
                return false;
            }

            CheckAircraft(model);
            if (string.IsNullOrEmpty (model.AcIndentified))
            {
                Logger.Log(LogLevel.Error, "IPCManager:WaitForSessionReady", $"No 787 Aircraft selected - aborting");
                return false;
            }

            return true;
        }

        public bool IsCamReady()
        {
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            short value = offCamReady.Value;

            return value < 11;
        }

        public void CloseSafe()
        {
            try
            {
                if (FSUIPCConnection.IsOpen)
                {
                    offInMenu.Disconnect();
                    offCamReady.Disconnect();
                    offAircraft.Disconnect();
                    FSUIPCConnection.Close();
                }

                if (!FSUIPCConnection.IsOpen)
                    Logger.Log(LogLevel.Information, "IPCManager:Close", $"FSUIPC Closed.");
                else
                    Logger.Log(LogLevel.Error, "IPCManager:Close", $"Failed to close FSUIPC!");
            }
            catch { }
        }
    }
}
