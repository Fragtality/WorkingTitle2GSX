using FSUIPC;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace WorkingTitle2GSX
{
    public class FlightPlan
    {
        public string Flight { get; set; } = "";
        public string FlightPlanID { get; set; } = "";
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string Units { get; set; }
        public double Fuel { get; set; }
        public int Passenger { get; set; }
        public int Bags { get; set; }
        public int CargoTotal { get; set; }
        public double WeightPax { get; set; }
        public double WeightBag { get; set; }
        private ServiceModel Model { get; set; }

        public FlightPlan(ServiceModel model)
        {
            Model = model;
        }

        protected async Task<string> GetHttpContent(HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync();
        }

        protected XmlNode FetchOnline()
        {
            if (Model.SimBriefID == "0")
            {
                Logger.Log(LogLevel.Error, "FlightPlan:FetchOnline", $"SimBrief ID is not set!");
                return null;
            }

            HttpClient httpClient = new();
            HttpResponseMessage response = httpClient.GetAsync(string.Format(Model.SimBriefURL, Model.SimBriefID)).Result;

            if (response.IsSuccessStatusCode)
            {
                string responseBody = GetHttpContent(response).Result;
                if (responseBody != null && responseBody.Length > 0)
                {
                    Logger.Log(LogLevel.Debug, "FlightPlan:FetchOnline", $"HTTP Request succeded!");
                    XmlDocument xmlDoc = new();
                    xmlDoc.LoadXml(responseBody);
                    return xmlDoc.ChildNodes[1];
                }
                else
                {
                    Logger.Log(LogLevel.Error, "FlightPlan:FetchOnline", $"SimBrief Response Body is empty!");
                }
            }
            else
            {
                Logger.Log(LogLevel.Error, "FlightPlan:FetchOnline", $"HTTP Request failed! Response Code: {response.StatusCode} Message: {response.ReasonPhrase}");
            }

            return null;
        }

        protected XmlNode LoadOFP()
        {
            return FetchOnline();
        }

        public bool Load()
        {
            XmlNode sbOFP = LoadOFP();
            if (sbOFP != null)
            {
                string lastID = FlightPlanID;
                Flight = sbOFP["general"]["icao_airline"].InnerText + sbOFP["general"]["flight_number"].InnerText;
                FlightPlanID = sbOFP["params"]["request_id"].InnerText;
                Origin = sbOFP["origin"]["icao_code"].InnerText;
                Destination = sbOFP["destination"]["icao_code"].InnerText;
                Units = sbOFP["params"]["units"].InnerText;
                Fuel = Convert.ToDouble(sbOFP["fuel"]["plan_ramp"].InnerText, new RealInvariantFormat(sbOFP["fuel"]["plan_ramp"].InnerText)); ;
                if (Model.UseActualPaxValue)
                {
                    Passenger = Convert.ToInt32(sbOFP["weights"]["pax_count_actual"].InnerText);
                    Bags = Convert.ToInt32(sbOFP["weights"]["bag_count_actual"].InnerText);
                }
                else
                {
                    Passenger = Convert.ToInt32(sbOFP["weights"]["pax_count"].InnerText);
                    Bags = Convert.ToInt32(sbOFP["weights"]["bag_count"].InnerText);
                }
                CargoTotal = Convert.ToInt32(sbOFP["weights"]["cargo"].InnerText);
                WeightPax = Convert.ToDouble(sbOFP["weights"]["pax_weight"].InnerText, new RealInvariantFormat(sbOFP["weights"]["pax_weight"].InnerText));
                WeightBag = Convert.ToDouble(sbOFP["weights"]["bag_weight"].InnerText, new RealInvariantFormat(sbOFP["weights"]["bag_weight"].InnerText));

                if (lastID != FlightPlanID && Model.AcIndentified == sbOFP["aircraft"]["name"].InnerText)
                {
                    Logger.Log(LogLevel.Information, "FlightPlan:Load", $"New OFP for Flight {Flight} loaded. ({Origin} -> {Destination})");
                }

                return lastID != FlightPlanID;
            }
            else
                return false;
        }

        public void SetPassengersGSX()
        {
            FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPASSENGERS", Passenger);
            if (Model.NoCrewBoarding)
            {
                FSUIPCConnection.WriteLVar("FSDT_GSX_CREW_NOT_DEBOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_CREW_NOT_BOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_PILOTS_NOT_DEBOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_PILOTS_NOT_BOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_NUMCREW", 9);
                FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPILOTS", 3);
                Logger.Log(LogLevel.Information, "FlightPlan:SetPassengersGSX", $"GSX Passengers set to {Passenger} (Crew Boarding disabled)");
            }
            else
                Logger.Log(LogLevel.Information, "FlightPlan:SetPassengersGSX", $"GSX Passengers set to {Passenger}");
        }
    }
}
