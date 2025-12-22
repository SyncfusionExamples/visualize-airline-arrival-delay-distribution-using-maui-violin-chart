using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection; 

namespace ViolinChart
{
    public class ViolinViewModel
    {
        public ObservableCollection<AirlineViolinModel> AirlineData { get; set; }
        public ObservableCollection<Brush> PaletteBrushesDark { get; set; } 
        public ViolinViewModel()
        { 
            PaletteBrushesDark = new ObservableCollection<Brush>()
            {
                new SolidColorBrush(Color.FromArgb("#00E5FF")),  
                new SolidColorBrush(Color.FromArgb("#7C4DFF")),  
                new SolidColorBrush(Color.FromArgb("#FF4081")),  
                new SolidColorBrush(Color.FromArgb("#FFC400")),  
                new SolidColorBrush(Color.FromArgb("#00E676")),  
            }; 

            AirlineData = new ObservableCollection<AirlineViolinModel>(); 
            LoadAllAirlineData();
            int count = 0;
            foreach(AirlineViolinModel i in AirlineData)
            {
                if (PaletteBrushesDark[count++] is SolidColorBrush col)
                    i.legendColor = col.Color;
            }
        }

        private void LoadAllAirlineData()
        { 
            var flightPoints = ReadCSV("Airline_Delay_Cause.csv"); 
            var groups = new Dictionary<string, List<double>>();

            foreach (var fp in flightPoints)
            { 
                if (fp.Cancelled || fp.Diverted) continue;
                 
                double delay = fp.ArrivalDelayMinutes;
                if (_clampEarlyToZero && delay < 0) delay = 0;
                if (_capMinutes.HasValue && delay > _capMinutes.Value) delay = _capMinutes.Value;

                if (!groups.TryGetValue(fp.Airline, out var list))
                {
                    list = new List<double>();
                    groups[fp.Airline] = list;
                }
                list.Add(delay);
            }
             
            var items = groups
                .Where(kvp => kvp.Value.Count >= _minSamples)
                .Select(kvp => new AirlineViolinModel(kvp.Key, kvp.Value))
                .OrderBy(m => m.Airline)
                .ToList();

            AirlineData.Clear();
            foreach (var item in items.Skip(4).Take(5))  
            {
                AirlineData.Add(item);
            }
        }
         
        private static IEnumerable<FlightPoint> ReadCSV(string fileName)
        {
            try
            { 
                Assembly executingAssembly = typeof(App).GetTypeInfo().Assembly;
                string resourcePath = $"ViolinChart.Resources.Raw.{fileName}";

                using Stream? inputStream = executingAssembly.GetManifestResourceStream(resourcePath);
                if (inputStream == null)
                { 
                    return Enumerable.Empty<FlightPoint>();
                }

                using StreamReader reader = new(inputStream);

                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                { 
                    return Enumerable.Empty<FlightPoint>();
                }

                var headers = SplitCsv(headerLine);
                int idxAirline = IndexOf(headers, "carrier_name");
                int idxDelay = IndexOf(headers, "arr_delay");
                int idxCancelled = IndexOf(headers, "arr_cancelled");
                int idxDiverted = IndexOf(headers, "arr_diverted");

                if (idxAirline < 0 || idxDelay < 0 || idxCancelled < 0 || idxDiverted < 0)
                {
                     return Enumerable.Empty<FlightPoint>();
                }

                var list = new List<FlightPoint>();
                string? line;
                int lineNumber = 1;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    try
                    {
                        var cols = SplitCsv(line);
                        if (cols.Length <= Math.Max(Math.Max(idxAirline, idxDelay), Math.Max(idxCancelled, idxDiverted)))
                            continue;

                        string airline = cols[idxAirline].Trim().Trim('"');
                        if (string.IsNullOrEmpty(airline)) continue;

                        if (!TryParseDouble(cols[idxDelay], out var delay)) continue;

                        bool cancelled = TryParseDouble(cols[idxCancelled], out var cVal) && cVal > 0;
                        bool diverted = TryParseDouble(cols[idxDiverted], out var dVal) && dVal > 0;

                        list.Add(new FlightPoint
                        {
                            Airline = airline,
                            ArrivalDelayMinutes = delay,
                            Cancelled = cancelled,
                            Diverted = diverted
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing line {lineNumber} in {fileName}: {ex.Message}");
                    }
                }
                 
                return list;
            }
            catch (Exception)
            { 
                return Enumerable.Empty<FlightPoint>();
            }
        }
         
        private class FlightPoint
        {
            public string Airline { get; set; } = "";
            public double ArrivalDelayMinutes { get; set; }
            public bool Cancelled { get; set; }
            public bool Diverted { get; set; }
        }
         
        private readonly bool _clampEarlyToZero = true;  
        private readonly int? _capMinutes = 240;          
        private readonly int _minSamples = 10;           
         
        private static int IndexOf(string[] headers, string name)
            => Array.FindIndex(headers, h => string.Equals(h.Trim().Trim('"'), name, StringComparison.OrdinalIgnoreCase));

        private static bool TryParseDouble(string s, out double value)
            => double.TryParse(s?.Trim().Trim('"'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
         
        private static string[] SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
