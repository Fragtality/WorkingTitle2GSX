using FSUIPC;
using System;

namespace WorkingTitle2GSX
{
    public class Aircraft
    {
        protected FlightPlan OFP { get; set; }
        protected ServiceModel Model { get; set; }

        protected double unitScalar;

        protected double fuelWingTarget;
        protected double fuelCenterTarget;
        protected double fuelWingCurrent;
        protected double fuelCenterCurrent;
        protected double fuelActiveTanks;
        protected Offset<int> fuelLeftOffset = new(ServiceModel.IpcGroupName, 0x0B7C);
        protected Offset<int> fuelRightOffset = new(ServiceModel.IpcGroupName, 0x0B94);
        protected Offset<int> fuelCenterOffset = new(ServiceModel.IpcGroupName, 0x0B74);

        protected Offset<ushort> timeAccel = new(ServiceModel.IpcGroupName, 0x0C1A);
        protected int infoTicksFuel = 0;
        protected double lastPax = -1;
        protected double lastCargo = -1;

        protected PayloadStations Stations = null;

        public Aircraft(ServiceModel model)
        {
            Model = model;
            LoadAircraft();
        }

        protected void LoadAircraft()
        {
            Stations = new PayloadStations(Model);
            timeAccel.Disconnect();

            Logger.Log(LogLevel.Debug, "IPCManager:CheckAircraft", $"Getting Fuel Information for {Model.AcIndentified}");
            Offset tankCapacityCenter = new(ServiceModel.IpcGroupName, 0x0B78, 4);
            Offset tankCapacityWing = new(ServiceModel.IpcGroupName, 0x0B80, 4);
            Offset offWeightConversion = new(ServiceModel.IpcGroupName, 0x0AF4, 2);
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            Model.ConstFuelWeight = offWeightConversion.GetValue<short>() * 0.00390625;
            Model.ConstMaxCenter = tankCapacityCenter.GetValue<int>();
            Model.ConstMaxWing = tankCapacityWing.GetValue<int>();
            tankCapacityCenter.Disconnect();
            tankCapacityWing.Disconnect();
            tankCapacityWing.Disconnect();
        }

        public void SetPayload(FlightPlan fPlan)
        {
            if (fPlan != null)
                OFP = fPlan;
            Logger.Log(LogLevel.Debug, "Aircraft:SetPayload", $"Setting Payload ...");

            Stations.SetWeights(OFP);
            if (OFP.Units != "kgs")
                unitScalar = 1.0;
            else
                unitScalar = Model.ConstKilo;

            Logger.Log(LogLevel.Debug, "Aircraft:SetPayload", $"Using Tank Capacity of {((Model.ConstMaxWing * 2 + Model.ConstMaxCenter) * Model.ConstFuelWeight * unitScalar):F0} {OFP.Units}");
            //FUEL
            fuelCenterTarget = OFP.Fuel - ((Model.ConstMaxWing * Model.ConstFuelWeight) * unitScalar * 2.0);
            if (fuelCenterTarget > 0)
                fuelWingTarget = (OFP.Fuel - fuelCenterTarget) / 2.0;
            else
            {
                fuelWingTarget = OFP.Fuel / 2.0;
                fuelCenterTarget = 0.0;
            }
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Wing Tanks Target: 2 * {fuelWingTarget:F0} {OFP.Units} | Center Target: {fuelCenterTarget:F0} {OFP.Units} | Total: {((fuelWingTarget * 2) + fuelCenterTarget):F0} {OFP.Units}");

            fuelWingTarget /= ((Model.ConstMaxWing * Model.ConstFuelWeight) * unitScalar);
            if (fuelWingTarget > 1.0)
                fuelWingTarget = 1.0;
            fuelCenterTarget /= ((Model.ConstMaxCenter * Model.ConstFuelWeight) * unitScalar);
            if (fuelCenterTarget > 1.0)
                fuelCenterTarget = 1.0;
                

            //PAX + CARGO
            double payloadPax = OFP.Passenger * OFP.WeightPax;
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Total Passengers: {OFP.Passenger} | Weight Pax: {payloadPax:F1} {OFP.Units}");
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Bags + Cargo: {OFP.CargoTotal:F1} {OFP.Units}");
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Total Payload: {(payloadPax + OFP.CargoTotal):F1} {OFP.Units}");
        }

        public void StartRefuel()
        {
            fuelLeftOffset.Reconnect();
            fuelRightOffset.Reconnect();
            fuelCenterOffset.Reconnect();
            if (!timeAccel.IsConnected)
                timeAccel.Reconnect();
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            
            double currentRight = (double)fuelRightOffset.Value / Model.ConstPercent;
            double currentCenter = (double)fuelCenterOffset.Value / Model.ConstPercent;
            fuelActiveTanks = 0;

            //Wings
            if (currentRight > fuelWingTarget)
            {
                fuelLeftOffset.Value = (int)Math.Round(fuelWingTarget * Model.ConstPercent, 0);
                fuelRightOffset.Value = (int)Math.Round(fuelWingTarget * Model.ConstPercent, 0);
                fuelWingCurrent = fuelWingTarget * 0.95;
                Logger.Log(LogLevel.Warning, "Aircraft:StartRefuel", $"Fuel currently in Wing Tanks higher than requested - reseted 95% of Target.");
            }
            else
            {
                fuelWingCurrent = currentRight;
                if (currentRight != fuelWingTarget)
                    fuelActiveTanks = 2;
            }            

            //Center
            if (fuelCenterTarget == 0.0 && fuelCenterOffset.Value != 0)
            {
                fuelCenterOffset.Value = 0;
                fuelCenterCurrent = 0.0;
                Logger.Log(LogLevel.Warning, "Aircraft:StartRefuel", $"Fuel left in Center Tank but Target is Zero - reseted to Zero.");
            }
            else if (fuelCenterTarget > 0.0 && currentCenter > fuelCenterTarget)
            {
                fuelCenterOffset.Value = (int)Math.Round(fuelCenterTarget * Model.ConstPercent, 0);
                fuelCenterCurrent = fuelCenterTarget;
                Logger.Log(LogLevel.Warning, "Aircraft:StartRefuel", $"Fuel in Center Tank higher than requested - reseted to planned Fuel.");
            }
            else if (fuelCenterTarget > 0.0)
            {
                fuelCenterCurrent = currentCenter;
                if (fuelCenterCurrent != fuelCenterTarget)
                    fuelActiveTanks++;
            }

            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            infoTicksFuel = 30;
            Logger.Log(LogLevel.Information, "Aircraft:StartRefuel", $"Refuel Service active ...");
        }

        public bool RefuelAircraft()
        {
            double accel = timeAccel.Value / 256.0;
            double tankWingStep = ((Model.GallonsPerSecond / fuelActiveTanks) * accel) / Model.ConstMaxWing;
            if (fuelWingCurrent < fuelWingTarget - tankWingStep)
            {
                fuelWingCurrent += tankWingStep;
                fuelLeftOffset.Value = (int)(fuelWingCurrent * Model.ConstPercent);
                fuelRightOffset.Value = (int)(fuelWingCurrent * Model.ConstPercent);
            }
            else if (fuelWingCurrent != fuelWingTarget)
            {
                fuelWingCurrent = fuelWingTarget;
                fuelLeftOffset.Value = (int)(fuelWingTarget * Model.ConstPercent);
                fuelRightOffset.Value = (int)(fuelWingTarget * Model.ConstPercent);
                fuelActiveTanks -= 2;
                Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings finished: {GetFuelWeight(fuelWingCurrent, Model.ConstMaxWing, 2)}");
            }

            double tankCenterStep = ((Model.GallonsPerSecond / fuelActiveTanks) * accel) / Model.ConstMaxCenter;
            if (fuelCenterTarget > 0.0)
            {
                if (fuelCenterCurrent < fuelCenterTarget - tankCenterStep)
                {
                    fuelCenterCurrent += tankCenterStep;
                    fuelCenterOffset.Value = (int)(fuelCenterCurrent * Model.ConstPercent);
                }
                else if (fuelCenterCurrent != fuelCenterTarget)
                {
                    fuelCenterCurrent = fuelCenterTarget;
                    fuelCenterOffset.Value = (int)(fuelCenterTarget * Model.ConstPercent);
                    fuelActiveTanks--;
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Center finished: {GetFuelWeight(fuelCenterCurrent, Model.ConstMaxCenter)}");
                }
            }

            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            if (infoTicksFuel >= 30 && fuelActiveTanks > 0)
            {
                if (fuelActiveTanks == 3)
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings refueling: {GetFuelWeight(fuelWingCurrent, Model.ConstMaxWing, 2)} {GetActualFlow(tankWingStep * 2.0, Model.ConstMaxWing)} | Center refueling: {GetFuelWeight(fuelCenterCurrent, Model.ConstMaxCenter)} {GetActualFlow(tankCenterStep, Model.ConstMaxCenter)}");
                else if (fuelActiveTanks == 2)
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings refueling: {GetFuelWeight(fuelWingCurrent, Model.ConstMaxWing, 2)} {GetActualFlow(tankWingStep * 2.0, Model.ConstMaxWing)}");
                else
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Center refueling: {GetFuelWeight(fuelCenterCurrent, Model.ConstMaxCenter)} {GetActualFlow(tankCenterStep, Model.ConstMaxCenter)}");
                infoTicksFuel = 0;
            }
            infoTicksFuel += 1 * (int)accel;

            return fuelActiveTanks == 0;
        }

        protected string GetFuelWeight(double percent, double capacity, int tanks = 1)
        {
            double level = percent * capacity * Model.ConstFuelWeight * unitScalar * tanks;
            return $"{level:F0} {OFP.Units}";
        }

        protected string GetActualFlow(double stepSize, double tankSize)
        {
            return string.Format("({0:F1}{1}/s)", (stepSize * tankSize) * Model.ConstFuelWeight * unitScalar, OFP.Units);
        }

        public void StopRefuel()
        {
            double left = (fuelLeftOffset.Value / Model.ConstPercent) * ((Model.ConstMaxWing * Model.ConstFuelWeight) * unitScalar);
            double right = (fuelRightOffset.Value / Model.ConstPercent) * ((Model.ConstMaxWing * Model.ConstFuelWeight) * unitScalar);
            double center = (fuelCenterOffset.Value / Model.ConstPercent) * ((Model.ConstMaxCenter * Model.ConstFuelWeight) * unitScalar);
            double sum = left + right + center;

            fuelLeftOffset.Disconnect();
            fuelRightOffset.Disconnect();
            fuelCenterOffset.Disconnect();

            Logger.Log(LogLevel.Information, "Aircraft:StopRefuel", $"Refuel finished! FOB: {sum:F1} {OFP.Units} (Wings: {(left + right):F1} {OFP.Units} | Center: {center:F1} {OFP.Units})");
        }

        public void StartBoarding()
        {
            Stations.Refresh();
            if (!timeAccel.IsConnected)
                timeAccel.Reconnect();

            Stations.SetPax(0);
            Stations.SetCargo(0);
            Stations.SetPilots();

            lastPax = -1;
            lastCargo = -1;
            Logger.Log(LogLevel.Information, "Aircraft:StartBoarding", $"Started Boarding (PAX: {OFP.Passenger}) ...");
        }

        public bool BoardAircraft()
        {
            int brdPax = (int)FSUIPCConnection.ReadLVar("FSDT_GSX_NUMPASSENGERS_BOARDING_TOTAL");
            double brdCargo = FSUIPCConnection.ReadLVar("FSDT_GSX_BOARDING_CARGO_PERCENT") / 100.0;

            bool changePax = false;
            if (brdPax != lastPax)
            {
                Stations.SetPax(brdPax);
                lastPax = brdPax;
                changePax = true;
            }

            bool changeCargo = false;
            if (brdCargo != lastCargo)
            {
                Stations.SetCargo(brdCargo);
                lastCargo = brdCargo;
                changeCargo = true;
            }

            if (changePax || changeCargo)
            { 
                if (changePax)
                    Logger.Log(LogLevel.Information, "Aircraft:BoardAircraft", $"Boarding Passenger ... {brdPax}/{OFP.Passenger}");
                if (changeCargo)
                    Logger.Log(LogLevel.Information, "Aircraft:BoardAircraft", $"Loading Cargo/Bags ... {(brdCargo * OFP.CargoTotal):F0}/{OFP.CargoTotal:F0} {OFP.Units}");
            }

            return brdPax == OFP.Passenger && brdCargo == 1.0;
        }

        public void StopBoarding()
        {
            int pax = Stations.GetPax();
            double cargo = Stations.GetCargo();

            timeAccel.Disconnect();

            Logger.Log(LogLevel.Information, "Aircraft:StopBoarding", $"Boarding finished! SOB: {pax} (Payload Total: {((double)pax * OFP.WeightPax) + cargo:F2} {OFP.Units})");
        }

        public void StartDeboarding()
        {
            Stations.Refresh();

            lastPax = -1;
            lastCargo = -1;
            Logger.Log(LogLevel.Information, "Aircraft:StartDeboarding", $"Started Deboarding (PAX: {OFP.Passenger}) ...");
        }

        public bool DeboardAircraft()
        {
            int debrdPax = OFP.Passenger - (int)FSUIPCConnection.ReadLVar("FSDT_GSX_NUMPASSENGERS_DEBOARDING_TOTAL");
            double debrdCargo = 1.0 - (FSUIPCConnection.ReadLVar("FSDT_GSX_DEBOARDING_CARGO_PERCENT") / 100.0);

            bool changePax = false;
            if (debrdPax != lastPax)
            {
                Stations.SetPax(debrdPax);
                lastPax = debrdPax;
                changePax = true;
            }

            bool changeCargo = false;
            if (debrdCargo != lastCargo)
            {
                Stations.SetCargo(debrdCargo);
                lastCargo = debrdCargo;
                changeCargo = true;
            }

            if (changePax || changeCargo)
            {
                if (changePax)
                    Logger.Log(LogLevel.Information, "Aircraft:StartDeboarding", $"Deboarding Passenger ... {debrdPax}/{OFP.Passenger}");
                if (changeCargo)
                    Logger.Log(LogLevel.Information, "Aircraft:StartDeboarding", $"Unloading Cargo/Pax ... {(debrdCargo * OFP.CargoTotal):F0}/{OFP.CargoTotal:F0} {OFP.Units}");
            }

            return debrdPax == 0 && debrdCargo == 0.0;
        }

        public void StopDeboarding()
        {
            Stations.SetPax(0);
            Stations.SetCargo(0);

            Logger.Log(LogLevel.Information, "Aircraft:StopDeboarding", $"Deboarding finished!");
        }

        public void SetEmpty()
        {
            Logger.Log(LogLevel.Information, "Aircraft:SetEmpty", $"Resetting Payload & Fuel (Empty Plane)");
            Stations.Refresh();
            fuelLeftOffset.Reconnect();
            fuelRightOffset.Reconnect();
            fuelCenterOffset.Reconnect();
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);

            Stations.SetPax(0);
            Stations.SetCargo(0);

            fuelLeftOffset.Value = (int)((Model.WingTankStartValue * Model.ConstPercent) / 100);
            fuelRightOffset.Value = (int)((Model.WingTankStartValue * Model.ConstPercent) / 100);
            fuelCenterOffset.Value = 0;

            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            fuelLeftOffset.Disconnect();
            fuelRightOffset.Disconnect();
            fuelCenterOffset.Disconnect();
        }
    }
}
