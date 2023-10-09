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

        protected double bizPax;
        protected double premPax;
        protected double ecoPax;
        protected double cargoFwd;
        protected double cargoAft;
        protected double payloadPax;
        protected double payloadCargo;
        protected Offset<double> paxBizOffset = new(ServiceModel.IpcGroupName, 0x1460);
        protected Offset<double> paxPremOffset = new(ServiceModel.IpcGroupName, 0x1490);
        protected Offset<double> paxEcoOffset = new(ServiceModel.IpcGroupName, 0x14C0);
        protected Offset<double> cargoFwdOffset = new(ServiceModel.IpcGroupName, 0x14F0);
        protected Offset<double> cargoAftOffset = new(ServiceModel.IpcGroupName, 0x1520);

        protected Offset<ushort> timeAccel = new(ServiceModel.IpcGroupName, 0x0C1A);
        protected int infoTicksFuel = 0;
        protected double lastPax = -1;
        protected double lastCargo = -1;


        public Aircraft(ServiceModel model)
        {
            Model = model;
            LoadAircraft();
        }

        protected void LoadAircraft()
        {
            SetClassScalar(Model.DistributionPax, Model.DistributionCargo);

            paxBizOffset.Disconnect();
            paxPremOffset.Disconnect();
            paxEcoOffset.Disconnect();
            cargoFwdOffset.Disconnect();
            cargoAftOffset.Disconnect();
            timeAccel.Disconnect();
        }

        protected void SetClassScalar(string distPax, string distCargo)
        {
            bizPax = Convert.ToInt32(distPax.Split(';')[0]) / 100.0;
            premPax = Convert.ToInt32(distPax.Split(';')[1]) / 100.0;
            ecoPax = Convert.ToInt32(distPax.Split(';')[2]) / 100.0;

            cargoFwd = Convert.ToInt32(distCargo.Split(';')[0]) / 100.0;
            cargoAft = Convert.ToInt32(distCargo.Split(';')[1]) / 100.0;
        }

        public void SetPayload(FlightPlan fPlan)
        {
            if (fPlan != null)
                OFP = fPlan;
            Logger.Log(LogLevel.Debug, "Aircraft:SetPayload", $"Setting Payload ...");

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
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Wing Tanks Target: {fuelWingTarget:F0} {OFP.Units} | Center Target: {fuelCenterTarget:F0} {OFP.Units} | Total: {((fuelWingTarget * 2) + fuelCenterTarget):F0} {OFP.Units}");

            fuelWingTarget /= ((Model.ConstMaxWing * Model.ConstFuelWeight) * unitScalar);
            if (fuelWingTarget > 1.0)
                fuelWingTarget = 1.0;
            fuelCenterTarget /= ((Model.ConstMaxCenter * Model.ConstFuelWeight) * unitScalar);
            if (fuelCenterTarget > 1.0)
                fuelCenterTarget = 1.0;
            

            //PAX + CARGO
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Total Passengers: {OFP.Passenger} (Business: {(OFP.Passenger * bizPax):F0} | Premium-Eco: {(OFP.Passenger * premPax):F0} | Economy: {(OFP.Passenger * ecoPax):F0})");
            payloadPax = OFP.Passenger * OFP.WeightPax;
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Weight Pax: {payloadPax:F1} {OFP.Units}");
            payloadCargo = OFP.CargoTotal;
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Bags + Cargo: {payloadCargo:F1} {OFP.Units}");
            Logger.Log(LogLevel.Information, "Aircraft:SetPayload", $"Total Payload: {(payloadPax + payloadCargo):F1} {OFP.Units}");
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
            Logger.Log(LogLevel.Information, "Aircraft:StartRefuel", $"Refuel Process started ...");
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
                Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings finished: {((fuelRightOffset.Value / Model.ConstPercent) * 100.0):F2}%");
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
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Center finished: {((fuelCenterOffset.Value / Model.ConstPercent) * 100.0):F2}%");
                }
            }

            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            if (infoTicksFuel >= 30 && fuelActiveTanks > 0)
            {
                if (fuelActiveTanks == 3)
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings refueling: {(fuelWingCurrent * 100):F2}% {GetActualFlow(tankWingStep * 2.0, Model.ConstMaxWing)} | Center refueling: {GetActualFlow(tankCenterStep, Model.ConstMaxCenter)}");
                else if (fuelActiveTanks == 2)
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Wings refueling: {(fuelWingCurrent * 100):F2}% {GetActualFlow(tankWingStep * 2.0, Model.ConstMaxWing)}");
                else
                    Logger.Log(LogLevel.Information, "Aircraft:RefuelAircraft", $"Center refueling: {(fuelCenterCurrent * 100):F2}% {GetActualFlow(tankCenterStep, Model.ConstMaxCenter)}");
                infoTicksFuel = 0;
            }
            infoTicksFuel += 1 * (int)accel;

            return fuelActiveTanks == 0;
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
            paxBizOffset.Reconnect();
            paxPremOffset.Reconnect();
            paxEcoOffset.Reconnect();
            cargoFwdOffset.Reconnect();
            cargoAftOffset.Reconnect();
            if (!timeAccel.IsConnected)
                timeAccel.Reconnect();

            paxBizOffset.Value = 0;
            paxPremOffset.Value = 0;
            paxEcoOffset.Value = 0;
            cargoFwdOffset.Value = 0;
            cargoAftOffset.Value = 0;
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);

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
                paxBizOffset.Value = brdPax * bizPax * (OFP.WeightPax / unitScalar);
                paxPremOffset.Value = brdPax * premPax * (OFP.WeightPax / unitScalar);
                paxEcoOffset.Value = brdPax * ecoPax * (OFP.WeightPax / unitScalar);
                lastPax = brdPax;
                changePax = true;
            }

            bool changeCargo = false;
            if (brdCargo != lastCargo)
            {
                cargoFwdOffset.Value = (brdCargo * cargoFwd) * (payloadCargo / unitScalar);
                cargoAftOffset.Value = (brdCargo * cargoAft) * (payloadCargo / unitScalar);
                lastCargo = brdCargo;
                changeCargo = true;
            }

            if (changePax || changeCargo)
            { 
                FSUIPCConnection.Process(ServiceModel.IpcGroupName);
                if (changePax)
                    Logger.Log(LogLevel.Information, "Aircraft:BoardAircraft", $"Boarding Passenger ... {brdPax}/{OFP.Passenger} (Business: {(brdPax * bizPax):F0} | Premium-Eco: {(brdPax * premPax):F0} | Economy: {(brdPax * ecoPax):F0})");
                if (changeCargo)
                    Logger.Log(LogLevel.Information, "Aircraft:BoardAircraft", $"Loading Cargo/Pax ... {(brdCargo * payloadCargo):F0}/{payloadCargo:F0} {OFP.Units} (Fwd: {(brdCargo * payloadCargo * cargoFwd):F0} | Aft: {(brdCargo * payloadCargo * cargoAft):F0})");
            }

            return brdPax == OFP.Passenger && brdCargo == 1.0;
        }

        public void StopBoarding()
        {
            double pax = (paxBizOffset.Value + paxPremOffset.Value + paxEcoOffset.Value) * unitScalar;
            double cargo = (cargoFwdOffset.Value + cargoAftOffset.Value) * unitScalar;

            paxBizOffset.Disconnect();
            paxPremOffset.Disconnect();
            paxEcoOffset.Disconnect();
            cargoFwdOffset.Disconnect();
            cargoAftOffset.Disconnect();
            timeAccel.Disconnect();

            Logger.Log(LogLevel.Information, "Aircraft:StopBoarding", $"Boarding finished! SOB: {(pax / OFP.WeightPax):F0} (Payload Total: {(pax + cargo):F2} {OFP.Units})");
        }

        public void StartDeboarding()
        {
            paxBizOffset.Reconnect();
            paxPremOffset.Reconnect();
            paxEcoOffset.Reconnect();
            cargoFwdOffset.Reconnect();
            cargoAftOffset.Reconnect();
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);

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
                paxBizOffset.Value = debrdPax * bizPax * (OFP.WeightPax / unitScalar);
                paxPremOffset.Value = debrdPax * premPax * (OFP.WeightPax / unitScalar);
                paxEcoOffset.Value = debrdPax * ecoPax * (OFP.WeightPax / unitScalar);
                lastPax = debrdPax;
                changePax = true;
            }

            bool changeCargo = false;
            if (debrdCargo != lastCargo)
            {
                cargoFwdOffset.Value = (debrdCargo * cargoFwd) * (payloadCargo / unitScalar);
                cargoAftOffset.Value = (debrdCargo * cargoAft) * (payloadCargo / unitScalar);
                lastCargo = debrdCargo;
                changeCargo = true;
            }

            if (changePax || changeCargo)
            {
                FSUIPCConnection.Process(ServiceModel.IpcGroupName);
                if (changePax)
                    Logger.Log(LogLevel.Information, "Aircraft:StartDeboarding", $"Deboarding Passenger ... {debrdPax}/{OFP.Passenger}");
                if (changeCargo)
                    Logger.Log(LogLevel.Information, "Aircraft:StartDeboarding", $"Unloading Cargo/Pax ... {(debrdCargo * payloadCargo):F0}/{payloadCargo:F0} {OFP.Units} (Fwd: {(debrdCargo * payloadCargo * cargoFwd):F0} | Aft: {(debrdCargo * payloadCargo * cargoAft):F0})");
            }

            return debrdPax == 0 && debrdCargo == 0.0;
        }

        public void StopDeboarding()
        {
            paxBizOffset.Value = 0;
            paxPremOffset.Value = 0;
            paxEcoOffset.Value = 0;
            cargoFwdOffset.Value = 0;
            cargoAftOffset.Value = 0;
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);

            paxBizOffset.Disconnect();
            paxPremOffset.Disconnect();
            paxEcoOffset.Disconnect();
            cargoFwdOffset.Disconnect();
            cargoAftOffset.Disconnect();

            Logger.Log(LogLevel.Information, "Aircraft:StopDeboarding", $"Deboarding finished!");
        }

        public void SetEmpty()
        {
            Logger.Log(LogLevel.Information, "Aircraft:SetEmpty", $"Resetting Paylod & Fuel (Empty Plane)");
            paxBizOffset.Reconnect();
            paxPremOffset.Reconnect();
            paxEcoOffset.Reconnect();
            cargoFwdOffset.Reconnect();
            cargoAftOffset.Reconnect();
            fuelLeftOffset.Reconnect();
            fuelRightOffset.Reconnect();
            fuelCenterOffset.Reconnect();
            FSUIPCConnection.Process(ServiceModel.IpcGroupName);

            paxBizOffset.Value = 0.0;
            paxPremOffset.Value = 0.0;
            paxEcoOffset.Value = 0.0;
            cargoFwdOffset.Value = 0.0;
            cargoAftOffset.Value = 0.0;

            fuelLeftOffset.Value = (int)((Model.WingTankStartValue * Model.ConstPercent) / 100);
            fuelRightOffset.Value = (int)((Model.WingTankStartValue * Model.ConstPercent) / 100);
            fuelCenterOffset.Value = 0;

            FSUIPCConnection.Process(ServiceModel.IpcGroupName);
            paxBizOffset.Disconnect();
            paxPremOffset.Disconnect();
            paxEcoOffset.Disconnect();
            cargoFwdOffset.Disconnect();
            cargoAftOffset.Disconnect();
            fuelLeftOffset.Disconnect();
            fuelRightOffset.Disconnect();
            fuelCenterOffset.Disconnect();
        }
    }
}
