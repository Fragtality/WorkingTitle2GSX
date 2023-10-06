using FSUIPC;
using Serilog;
using System;

namespace WorkingTitle2GSX
{
    public class Aircraft
    {
        protected FlightPlan OFP { get; set; }

        protected double unitScalar;

        protected double fuelWingTarget;
        protected double fuelCenterTarget;
        protected double fuelWingCurrent;
        protected double fuelCenterCurrent;
        protected double fuelActiveTanks;
        protected Offset<int> fuelLeftOffset = new Offset<int>(Program.groupName, 0x0B7C);
        protected Offset<int> fuelRightOffset = new Offset<int>(Program.groupName, 0x0B94);
        protected Offset<int> fuelCenterOffset = new Offset<int>(Program.groupName, 0x0B74);

        protected double bizPax;
        protected double premPax;
        protected double ecoPax;
        protected double cargoFwd;
        protected double cargoAft;
        protected double payloadPax;
        protected double payloadCargo;
        protected Offset<double> paxBizOffset = new Offset<double>(Program.groupName, 0x1460);
        protected Offset<double> paxPremOffset = new Offset<double>(Program.groupName, 0x1490);
        protected Offset<double> paxEcoOffset = new Offset<double>(Program.groupName, 0x14C0);
        protected Offset<double> cargoFwdOffset = new Offset<double>(Program.groupName, 0x14F0);
        protected Offset<double> cargoAftOffset = new Offset<double>(Program.groupName, 0x1520);

        protected Offset<ushort> timeAccel = new Offset<ushort>(Program.groupName, 0x0C1A);
        protected int infoTicksFuel = 0;
        protected double lastPax = -1;
        protected double lastCargo = -1;


        public Aircraft()
        {
            LoadAircraft();
        }

        public Aircraft(FlightPlan fPlan)
        {
            LoadAircraft();
            OFP = fPlan;
            SetPayload(null);
        }

        protected void LoadAircraft()
        {
            SetClassScalar(Program.constPaxDistPercent, Program.constCargoDistPercent);

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
            Log.Logger.Information("Setting Payload ...");

            if (OFP.Units != "kgs")
                unitScalar = 1.0;
            else
                unitScalar = Program.constKilo;

            Log.Logger.Debug($"Using Tank Capacity of {((Program.constMaxWing * 2 + Program.constMaxCenter) * Program.constFuelWeight * unitScalar):F0} {OFP.Units}");
            //FUEL
            fuelCenterTarget = OFP.Fuel - ((Program.constMaxWing * Program.constFuelWeight) * unitScalar * 2.0);
            if (fuelCenterTarget > 0)
                fuelWingTarget = (OFP.Fuel - fuelCenterTarget) / 2.0;
            else
            {
                fuelWingTarget = OFP.Fuel / 2.0;
                fuelCenterTarget = 0.0;
            }
            Log.Logger.Information($"Wing Target: {fuelWingTarget:F0} {OFP.Units} | Center Target: {fuelCenterTarget:F0} {OFP.Units} | Total: {((fuelWingTarget * 2) + fuelCenterTarget):F0} {OFP.Units}");

            fuelWingTarget /= ((Program.constMaxWing * Program.constFuelWeight) * unitScalar);
            if (fuelWingTarget > 1.0)
                fuelWingTarget = 1.0;
            fuelCenterTarget /= ((Program.constMaxCenter * Program.constFuelWeight) * unitScalar);
            if (fuelCenterTarget > 1.0)
                fuelCenterTarget = 1.0;
            Log.Logger.Information($"Wing Target: {(fuelWingTarget * 100):F2}% | Center Target: {(fuelCenterTarget * 100):F2}%");          


            //PAX + CARGO
            Log.Logger.Information($"Total Passengers: {OFP.Passenger} (Business: {(OFP.Passenger * bizPax):F0} | Premium-Eco: {(OFP.Passenger * premPax):F0} | Economy: {(OFP.Passenger * ecoPax):F0} | Bags: {OFP.Bags})");
            payloadPax = OFP.Passenger * OFP.WeightPax;
            Log.Logger.Information($"Weight Passenger: {payloadPax:F1} {OFP.Units}");
            payloadCargo = OFP.CargoTotal;
            Log.Logger.Information($"Bags + Cargo: {payloadCargo:F1} {OFP.Units}");
            Log.Logger.Information($"Total Payload: {(payloadPax + payloadCargo):F1} {OFP.Units}");
        }

        public void StartRefuel()
        {
            fuelLeftOffset.Reconnect();
            fuelRightOffset.Reconnect();
            fuelCenterOffset.Reconnect();
            if (!timeAccel.IsConnected)
                timeAccel.Reconnect();
            FSUIPCConnection.Process(Program.groupName);
            
            double currentRight = (double)fuelRightOffset.Value / Program.constPercent;
            double currentCenter = (double)fuelCenterOffset.Value / Program.constPercent;
            fuelActiveTanks = 0;

            //Wings
            if (currentRight > fuelWingTarget)
            {
                fuelLeftOffset.Value = (int)Math.Round(fuelWingTarget * Program.constPercent, 0);
                fuelRightOffset.Value = (int)Math.Round(fuelWingTarget * Program.constPercent, 0);
                fuelWingCurrent = fuelWingTarget;
                Log.Logger.Information("Fuel currently in Wing Tanks higher than requested - reseted to planned Fuel.");
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
                Log.Logger.Information("Fuel left in Center Tank - reseted to Zero.");
            }
            else if (fuelCenterTarget > 0.0 && currentCenter > fuelCenterTarget)
            {
                fuelCenterOffset.Value = (int)Math.Round(fuelCenterTarget * Program.constPercent, 0);
                fuelCenterCurrent = fuelCenterTarget;
                Log.Logger.Information("Fuel currently in Center Tank higher than requested - reseted to planned Fuel.");
            }
            else if (fuelCenterTarget > 0.0)
            {
                fuelCenterCurrent = currentCenter;
                if (fuelCenterCurrent != fuelCenterTarget)
                    fuelActiveTanks++;
            }

            FSUIPCConnection.Process(Program.groupName);
            infoTicksFuel = 30;
            Log.Logger.Information("Refuel Process started ...");
        }

        public bool RefuelAircraft()
        {
            double accel = timeAccel.Value / 256.0;
            double tankWingStep = ((Program.constGPS / fuelActiveTanks) * accel) / Program.constMaxWing;
            if (fuelWingCurrent < fuelWingTarget - tankWingStep)
            {
                fuelWingCurrent += tankWingStep;
                fuelLeftOffset.Value = (int)(fuelWingCurrent * Program.constPercent);
                fuelRightOffset.Value = (int)(fuelWingCurrent * Program.constPercent);
            }
            else if (fuelWingCurrent != fuelWingTarget)
            {
                fuelWingCurrent = fuelWingTarget;
                fuelLeftOffset.Value = (int)(fuelWingTarget * Program.constPercent);
                fuelRightOffset.Value = (int)(fuelWingTarget * Program.constPercent);
                fuelActiveTanks -= 2;
                Log.Logger.Information($"Wings finished: {((fuelRightOffset.Value / Program.constPercent) * 100.0):F2}%");
            }

            double tankCenterStep = ((Program.constGPS / fuelActiveTanks) * accel) / Program.constMaxCenter;
            if (fuelCenterTarget > 0.0)
            {
                if (fuelCenterCurrent < fuelCenterTarget - tankCenterStep)
                {
                    fuelCenterCurrent += tankCenterStep;
                    fuelCenterOffset.Value = (int)(fuelCenterCurrent * Program.constPercent);
                }
                else if (fuelCenterCurrent != fuelCenterTarget)
                {
                    fuelCenterCurrent = fuelCenterTarget;
                    fuelCenterOffset.Value = (int)(fuelCenterTarget * Program.constPercent);
                    fuelActiveTanks--;
                    Log.Logger.Information($"Center finished: {((fuelCenterOffset.Value / Program.constPercent) * 100.0):F2}%");
                }
            }

            FSUIPCConnection.Process(Program.groupName);
            if (infoTicksFuel >= 30 && fuelActiveTanks > 0)
            {
                if (fuelActiveTanks == 3)
                    Log.Logger.Information($"Wings refueling: {(fuelWingCurrent * 100):F2}% {GetActualFlow(tankWingStep * 2.0, Program.constMaxWing)} | Center refueling: {GetActualFlow(tankCenterStep, Program.constMaxCenter)}");
                else if (fuelActiveTanks == 2)
                    Log.Logger.Information($"Wings refueling: {(fuelWingCurrent * 100):F2}% {GetActualFlow(tankWingStep * 2.0, Program.constMaxWing)}");
                else
                    Log.Logger.Information($"Center refueling: {(fuelCenterCurrent * 100):F2}% {GetActualFlow(tankCenterStep, Program.constMaxCenter)}");
                infoTicksFuel = 0;
            }
            infoTicksFuel += 1 * (int)accel;

            return fuelActiveTanks == 0;
        }

        protected string GetActualFlow(double stepSize, double tankSize)
        {
            return string.Format("({0:F1}{1}/s)", (stepSize * tankSize) * Program.constFuelWeight * unitScalar, OFP.Units);
        }

        public void StopRefuel()
        {
            double left = (fuelLeftOffset.Value / Program.constPercent) * ((Program.constMaxWing * Program.constFuelWeight) * unitScalar);
            double right = (fuelRightOffset.Value / Program.constPercent) * ((Program.constMaxWing * Program.constFuelWeight) * unitScalar);
            double center = (fuelCenterOffset.Value / Program.constPercent) * ((Program.constMaxCenter * Program.constFuelWeight) * unitScalar);
            double sum = left + right + center;

            fuelLeftOffset.Disconnect();
            fuelRightOffset.Disconnect();
            fuelCenterOffset.Disconnect();

            Log.Logger.Information($"Refuel finished! FOB: {sum:F1} {OFP.Units} (Wings: {(left + right):F1} {OFP.Units} | Center: {center:F1} {OFP.Units})");
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
            FSUIPCConnection.Process(Program.groupName);

            lastPax = -1;
            lastCargo = -1;
            Log.Logger.Information($"Started Boarding (PAX: {OFP.Passenger}) ...");
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
                FSUIPCConnection.Process(Program.groupName);
                if (changePax)
                    Log.Logger.Information($"Boarding Passenger ... {brdPax}/{OFP.Passenger} (Business: {(brdPax * bizPax):F0} | Premium-Eco: {(brdPax * premPax):F0} | Economy: {(brdPax * ecoPax):F0})");
                if (changeCargo)
                    Log.Logger.Information($"Loading Cargo/Pax ... {(brdCargo * payloadCargo):F0}/{payloadCargo:F0} {OFP.Units} (Fwd: {(brdCargo * payloadCargo * cargoFwd):F0} | Aft: {(brdCargo * payloadCargo * cargoAft):F0})");
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

            Log.Logger.Information($"Boarding finished! SOB: {(pax / OFP.WeightPax):F0} (Payload Total: {(pax + cargo):F2} {OFP.Units})");
        }

        public void StartDeboarding()
        {
            paxBizOffset.Reconnect();
            paxPremOffset.Reconnect();
            paxEcoOffset.Reconnect();
            cargoFwdOffset.Reconnect();
            cargoAftOffset.Reconnect();
            FSUIPCConnection.Process(Program.groupName);

            lastPax = -1;
            lastCargo = -1;
            Log.Logger.Information($"Started Deboarding (PAX: {OFP.Passenger}) ...");
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
                FSUIPCConnection.Process(Program.groupName);
                if (changePax)
                    Log.Logger.Information($"Deboarding Passenger ... {debrdPax}/{OFP.Passenger}");
                if (changeCargo)
                    Log.Logger.Information($"Unloading Cargo/Pax ... {(debrdCargo * payloadCargo):F0}/{payloadCargo:F0} {OFP.Units} (Fwd: {(debrdCargo * payloadCargo * cargoFwd):F0} | Aft: {(debrdCargo * payloadCargo * cargoAft):F0})");
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
            FSUIPCConnection.Process(Program.groupName);

            paxBizOffset.Disconnect();
            paxPremOffset.Disconnect();
            paxEcoOffset.Disconnect();
            cargoFwdOffset.Disconnect();
            cargoAftOffset.Disconnect();

            Log.Logger.Information($"Deboarding finished!");
        }

        public void SetEmpty()
        {
            Log.Logger.Information($"Resetting Paylod & Fuel (Empty Plane)");
            paxBizOffset.Reconnect();
            paxPremOffset.Reconnect();
            paxEcoOffset.Reconnect();
            cargoFwdOffset.Reconnect();
            cargoAftOffset.Reconnect();
            fuelLeftOffset.Reconnect();
            fuelRightOffset.Reconnect();
            fuelCenterOffset.Reconnect();
            FSUIPCConnection.Process(Program.groupName);

            paxBizOffset.Value = 0.0;
            paxPremOffset.Value = 0.0;
            paxEcoOffset.Value = 0.0;
            cargoFwdOffset.Value = 0.0;
            cargoAftOffset.Value = 0.0;

            fuelLeftOffset.Value = (int)((Program.startFuelWingPercent * Program.constPercent) / 100);
            fuelRightOffset.Value = (int)((Program.startFuelWingPercent * Program.constPercent) / 100);
            fuelCenterOffset.Value = 0;

            FSUIPCConnection.Process(Program.groupName);
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
