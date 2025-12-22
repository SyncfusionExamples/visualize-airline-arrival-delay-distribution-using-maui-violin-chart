namespace ViolinChart
{
    public class AirlineViolinModel
    { 
        public AirlineViolinModel(string airline, List<double> arrivalDelayMinutes)
        {
            Airline = airline;
            ArrivalDelayMinutes = arrivalDelayMinutes;
        }

        public string Airline { get; set; } 
        public Color? legendColor { get; set; }
        public List<double> ArrivalDelayMinutes { get; }
    }
}
