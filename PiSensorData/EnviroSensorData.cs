using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiSensorData
{
    public class EnviroSensorData
    {
        public string Temperature { get; set; }
        public string Pressure { get; set; }
        public string Humidity { get; set; }

        public string Altitude { get; set; }
        public string Light { get; set; }
        public string Proximity { get; set; }

        public override string ToString()
        {
            return $"Temperature: {Temperature}\n" +
                    $"Pressure: {Pressure}\n" +
                    $"Relative humidity: {Humidity}\n" +
                    $"Estimated altitude: {Altitude}\n" +
                    $"Light: {Light} lux\n" +
                    $"Proximity: {Proximity}\n";
        }
    }
}
