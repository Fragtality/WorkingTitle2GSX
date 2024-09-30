using FSUIPC;
using System;
using System.Collections.Generic;

namespace WorkingTitle2GSX
{
    public class PayloadStations
    {
        protected ServiceModel Model = null;
        protected double paxWeight = 187;
        protected double unitScalar = 1.0;
        protected Dictionary<int, double> paxStations = new();
        protected List<KeyValuePair<int, double>> cargoStations = new();
        protected double payloadCargo;

        public PayloadStations(ServiceModel model)
        {
            SetDistribution(model);
        }

        protected void SetDistribution(ServiceModel model)
        {
            Model = model;
            SelectDistribution(model, out string distPax, out string distCargo);

            paxStations.Clear();
            string[] stations = distPax.Split(';');
            foreach (string station in stations)
            {
                string[] parts = station.Split("=");
                paxStations.Add(Convert.ToInt32(parts[0]) - 1, Convert.ToDouble(parts[1], new RealInvariantFormat(parts[1])));
            }

            cargoStations.Clear();
            stations = distCargo.Split(';');
            foreach (string station in stations)
            {
                string[] parts = station.Split("=");
                cargoStations.Add(new KeyValuePair<int, double>(Convert.ToInt32(parts[0]) - 1, Convert.ToDouble(parts[1], new RealInvariantFormat(parts[1]))));
            }
        }
        public void SetWeights(FlightPlan OFP)
        {
            if (OFP.Units != "kgs")
                unitScalar = 1.0;
            else
                unitScalar = Model.ConstKilo;

            paxWeight = OFP.WeightPax / unitScalar;
            payloadCargo = OFP.CargoTotal / unitScalar;
        }

        public static void SelectDistribution(ServiceModel model, out string distPax, out string distCargo)
        {
            if (model.AcIndentified.Contains("HorizonSim_B787_9", StringComparison.InvariantCultureIgnoreCase))
            {
                distPax = model.DistributionPaxHS;
                distCargo = model.DistributionCargoHS;
            }
            else if (model.AcIndentified.Contains("Kuro_B787_8", StringComparison.InvariantCultureIgnoreCase))
            {
                distPax = model.DistributionPaxKU;
                distCargo = model.DistributionCargoKU;
            }
            else if (model.AcIndentified.Contains("B787_9", StringComparison.InvariantCultureIgnoreCase))
            {
                distPax = model.DistributionPaxWT;
                distCargo = model.DistributionCargoWT;
            }
            else //"Asobo_B787_10"
            {
                distPax = model.DistributionPaxWT;
                distCargo = model.DistributionCargoWT;
            }
        }

        public void Refresh()
        {
            FSUIPCConnection.PayloadServices.RefreshData();
        }
        public void SetPilots()
        {
            FSUIPCConnection.PayloadServices.PayloadStations[0].WeightLbs = paxWeight;
            FSUIPCConnection.PayloadServices.PayloadStations[1].WeightLbs = paxWeight;
        }
        public void SetPax(int num)
        {
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            double weightPax;
            double weightPilots;
            foreach (var station in paxStations)
            {
                weightPax = (double)num * paxWeight * station.Value;
                weightPilots = 2 * (85 / Model.ConstKilo) * station.Value;
                if (weightPax - weightPilots >= 0)
                {
                    Logger.Log(LogLevel.Debug, "PayloadStations:SetPax", $"Setting {weightPax - weightPilots}lbs ({(weightPax - weightPilots) * Model.ConstKilo}kg) to Station '{station.Key}' ({ipcStations[station.Key].Name})");
                    ipcStations[station.Key].WeightLbs = weightPax - weightPilots;
                }
                else
                {
                    Logger.Log(LogLevel.Debug, "PayloadStations:SetPax", $"Setting {weightPax}lbs ({weightPax * Model.ConstKilo}kg) to Station '{station.Key}' ({ipcStations[station.Key].Name})");
                    ipcStations[station.Key].WeightLbs = weightPax;
                }
            }
            FSUIPCConnection.PayloadServices.WriteChanges();
        }
        public int GetPax()
        {
            FSUIPCConnection.PayloadServices.RefreshData();
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            double result = 0;
            foreach (var station in paxStations)
            {
                result += ipcStations[station.Key].WeightLbs / paxWeight;
            }

            return (int)(Math.Round(result,0));
        }

        public void SetCargo(double percent)
        {
            if (Model.AcIndentified.Contains("HorizonSim_B787_9", StringComparison.InvariantCultureIgnoreCase))
            {
                SetCargoHS(percent);
            }
            else if (Model.AcIndentified.Contains("Kuro_B787_8", StringComparison.InvariantCultureIgnoreCase))
            {
                SetCargoKU(percent);
            }
            else if (Model.AcIndentified.Contains("B787_9", StringComparison.InvariantCultureIgnoreCase))
            {
                SetCargoWT(percent);
            }
            else //"Asobo_B787_10"
            {
                SetCargoWT(percent);
            }
            FSUIPCConnection.PayloadServices.WriteChanges();
        }

        private void SetCargoHS(double percent)
        {
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            if (percent * payloadCargo < Model.RearCargoMaxHS)
            {
                Logger.Log(LogLevel.Debug, "PayloadStations:SetCargoHS", $"Setting {percent * payloadCargo}lbs ({(percent * payloadCargo) * Model.ConstKilo}kg) to Station '{cargoStations[1].Key}' ({ipcStations[cargoStations[1].Key].Name})");
                ipcStations[cargoStations[1].Key].WeightLbs = percent * payloadCargo;
            }
            else
            {
                Logger.Log(LogLevel.Debug, "PayloadStations:SetCargoHS", $"Setting {Model.RearCargoMaxHS}lbs ({Model.RearCargoMaxHS * Model.ConstKilo}kg) to Station '{cargoStations[1].Key}' ({ipcStations[cargoStations[1].Key].Name})");
                ipcStations[cargoStations[1].Key].WeightLbs = Model.RearCargoMaxHS;
                Logger.Log(LogLevel.Debug, "PayloadStations:SetCargoHS", $"Setting {(percent * payloadCargo) - Model.RearCargoMaxHS}lbs ({((percent * payloadCargo) - Model.RearCargoMaxHS) * Model.ConstKilo}kg) to Station '{cargoStations[0].Key}' ({ipcStations[cargoStations[0].Key].Name})");
                ipcStations[cargoStations[0].Key].WeightLbs = (percent * payloadCargo) - Model.RearCargoMaxHS;
            }
        }

        private void SetCargoKU(double percent)
        {
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            if (percent * payloadCargo * 0.5 < Model.RearCargoMaxKU)
            {
                ipcStations[cargoStations[0].Key].WeightLbs = percent * payloadCargo * 0.5;
                ipcStations[cargoStations[1].Key].WeightLbs = percent * payloadCargo * 0.5;
            }
            else
            {
                ipcStations[cargoStations[1].Key].WeightLbs = Model.RearCargoMaxKU;
                ipcStations[cargoStations[0].Key].WeightLbs = (percent * payloadCargo) - Model.RearCargoMaxKU;
            }
        }

        private void SetCargoWT(double percent)
        {
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            foreach (var station in cargoStations)
                ipcStations[station.Key].WeightLbs = percent * payloadCargo * station.Value;
        }

        public double GetCargo()
        {
            FSUIPCConnection.PayloadServices.RefreshData();
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            double result = 0;
            foreach (var station in cargoStations)
            {
                result += ipcStations[station.Key].WeightLbs * unitScalar;
            }

            return Math.Round(result, 0);
        }
    }
}
