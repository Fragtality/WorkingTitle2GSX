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

        public PayloadStations(FlightPlan OFP, ServiceModel model)
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

        //public static PayloadStations CreateInstance(string aircraft, FlightPlan OFP, ServiceModel model)
        //{
        //    if (aircraft.Contains("HS"))
        //        return new PayloadStationsHS(OFP, model);
        //    else if (aircraft.Contains("WT") || aircraft.Contains("4S") || aircraft.Contains("KR"))
        //        return new PayloadStationsWT(OFP, model);
        //    else
        //        return null;
        //}
        public static void SelectDistribution(ServiceModel model, out string distPax, out string distCargo)
        {
            if (model.AcIndentified.Contains("HorizonSim_B787_9"))
            {
                distPax = model.DistributionPaxHS;
                distCargo = model.DistributionCargoHS;
            }
            else if (model.AcIndentified.Contains("Asobo_B787_10") || model.AcIndentified.Contains("B787_9") || model.AcIndentified.Contains("Kuro_B787_8"))
            {
                distPax = model.DistributionPaxWT;
                distCargo = model.DistributionCargoWT;
            }
            else
            {
                distPax = model.DistributionPaxWT;
                distCargo = model.DistributionCargoWT;
            }
        }
        //public void Disconnect()
        //{
        //    FSUIPCConnection.PayloadServices.WriteChanges();
        //}
        public void Refresh()
        {
            FSUIPCConnection.PayloadServices.RefreshData();
        }
        public void SetPax(int num)
        {
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            foreach (var station in paxStations)
            {
                ipcStations[station.Key].WeightLbs = (double)num * paxWeight * station.Value;
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
            var ipcStations = FSUIPCConnection.PayloadServices.PayloadStations;
            if (percent * payloadCargo * cargoStations[1].Value < 21100)
            {
                ipcStations[cargoStations[1].Key].WeightLbs = percent * payloadCargo;
                if (percent == 0)
                    ipcStations[cargoStations[0].Key].WeightLbs = 0;
            }
            else
            {
                foreach (var station in cargoStations)
                    ipcStations[station.Key].WeightLbs = percent * payloadCargo * station.Value;
            }
            FSUIPCConnection.PayloadServices.WriteChanges();
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
