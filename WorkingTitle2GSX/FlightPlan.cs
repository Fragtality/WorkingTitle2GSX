using FSUIPC;
using Serilog;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace WorkingTitle2GSX
{
    public class FlightPlan
    {
        public string Flight { get; set; } = "";
        public string Origin { get; set; }
        public string Destination { get; set; }
        public string Units { get; set; }
        public double Fuel { get; set; }
        public int Passenger { get; set; }
        public int Bags { get; set; }
        public int CargoTotal { get; set; }
        public double WeightPax { get; set; }
        public double WeightBag { get; set; }

        public FlightPlan()
        {
            
        }

        protected async Task<string> GetHttpContent(HttpResponseMessage response)
        {
            return await response.Content.ReadAsStringAsync();
        }

        protected XmlNode FetchOnline()
        {
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage response = httpClient.GetAsync(string.Format(Program.simbriefURL, Program.pilotID)).Result;

            if (response.IsSuccessStatusCode)
            {
                string responseBody = GetHttpContent(response).Result;
                if (responseBody != null && responseBody.Length > 0)
                {
                    Log.Logger.Debug($"HTTP Request succeded!");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(responseBody);
                    return xmlDoc.ChildNodes[1];
                }
                else
                {
                    Log.Logger.Error("Response Body is empty!");
                }
            }
            else
            {
                Log.Logger.Error($"HTTP Request failed! Response Code: {response.StatusCode} Message: {response.ReasonPhrase}");
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
            string lastFlight = Flight;
            Flight = sbOFP["general"]["icao_airline"].InnerText + sbOFP["general"]["flight_number"].InnerText;
            Origin = sbOFP["origin"]["icao_code"].InnerText;
            Destination = sbOFP["destination"]["icao_code"].InnerText;
            Units = sbOFP["params"]["units"].InnerText;
            Fuel = Convert.ToDouble(sbOFP["fuel"]["plan_ramp"].InnerText, new RealInvariantFormat(sbOFP["fuel"]["plan_ramp"].InnerText)); ;
            if (Program.useActualValue)
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

            if (lastFlight != Flight && Program.acIdentified == sbOFP["aircraft"]["name"].InnerText)
            {
                Log.Logger.Information($"New OFP for Flight {Flight} loaded. ({Origin} -> {Destination})");
            }

            return lastFlight != Flight;
        }

        public void SetPassengersGSX()
        {
            FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPASSENGERS", Passenger);
            if (Program.noCrewBoarding)
            {
                FSUIPCConnection.WriteLVar("FSDT_GSX_CREW_NOT_DEBOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_CREW_NOT_BOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_PILOTS_NOT_DEBOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_PILOTS_NOT_BOARDING", 1);
                FSUIPCConnection.WriteLVar("FSDT_GSX_NUMCREW", 9);
                FSUIPCConnection.WriteLVar("FSDT_GSX_NUMPILOTS", 3);
            }
        }
    }
}
