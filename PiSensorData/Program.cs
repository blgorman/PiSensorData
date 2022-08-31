using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Configuration;
using System.Device.I2c;
using System.Diagnostics;
using System.Text;

namespace PiSensorData
{
    public class Program
    {
        private static IConfigurationRoot _configuration;
        private static DeviceClient _deviceClient;
        private static bool _shouldShowTelemetryOutput = true;
        private static string _deviceConnectionString = "";
        private static int _telemetryReadForSeconds = 15;

        public static async Task Main(string[] args)
        {
            BuildOptions();
            BuildConfigValues();
            await ReadSensorData();

            Console.WriteLine("Program Completed");
        }

        private static string GetConfigValue(string variableKey)
        {
            var variableValue = Environment.GetEnvironmentVariable(variableKey);
            if (string.IsNullOrWhiteSpace(variableValue))
            {
                variableValue = _configuration[variableKey];
            }

            return variableValue;
        }

        private static void BuildConfigValues()
        {
            _shouldShowTelemetryOutput = Convert.ToBoolean(GetConfigValue("Device:OutputTelemetry"));
            Console.WriteLine($"Show Telemetry: {_shouldShowTelemetryOutput}");
            //get device connection string
            _deviceConnectionString = GetConfigValue("Device:AzureConnectionString");
            var keyIndex = _deviceConnectionString.IndexOf("SharedAccessKey");
            var safeShowConStr = _deviceConnectionString.Substring(0, keyIndex);
            safeShowConStr += "SharedAccessKey=*****************";
            Console.WriteLine($"Connection string: {safeShowConStr}");

            //get configured read duration [default/min => 15 seconds]
            var duration = GetConfigValue("Device:TelemetryReadDurationInSeconds");
            Console.WriteLine($"Telemetry Read Duration set to: {duration} seconds");

            //update duration from value if > 15
            if (!string.IsNullOrWhiteSpace(duration))
            {
                int.TryParse(duration, out int readDurationSeconds);
                if (readDurationSeconds > 15)
                {
                    _telemetryReadForSeconds = readDurationSeconds;
                }
            }
        }

        private static async Task ReadSensorData()
        {
            //set up the device client
            _deviceClient = DeviceClient.CreateFromConnectionString(
                    _deviceConnectionString,
                    TransportType.Mqtt);

            //set time to end reading data
            var endReadingsAtTime = DateTime.Now.AddSeconds(_telemetryReadForSeconds);

            //utilize the library to read Bme280 data
            var i2cSettings = new I2cConnectionSettings(1, Bme280.SecondaryI2cAddress);
            using I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
            using var bme280 = new Bme280(i2cDevice);

            //device readings created by python script execution on the device:
            int measurementTime = bme280.GetMeasurementDuration();
            var command = "python";
            var script = @"/home/pi/enviro/enviroplus-python/examples/singlelight.py";
            var args = $"{script}";
            //loop until duration
            while (DateTime.Now < endReadingsAtTime)
            {
                bme280.SetPowerMode(Bmx280PowerMode.Forced);
                Thread.Sleep(measurementTime);

                //read values for temp/pressure/humidity/altitude
                bme280.TryReadTemperature(out var tempValue);
                bme280.TryReadPressure(out var preValue);
                bme280.TryReadHumidity(out var humValue);
                bme280.TryReadAltitude(out var altValue);

                //set base values:
                var envData = new EnviroSensorData();
                envData.Temperature = $"{tempValue.DegreesCelsius:0.#}\u00B0C";
                envData.Humidity = $"{humValue.Percent:#.##}%";
                envData.Pressure = $"{preValue.Hectopascals:#.##} hPa";
                envData.Altitude = $"{altValue.Meters:#} m";

                //read light and proximity values
                string lightProx = string.Empty;
                using (Process process = new Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = command;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();

                    StreamReader sr = process.StandardOutput;
                    lightProx = sr.ReadToEnd();
                    process.WaitForExit();
                }

                var result = lightProx.Split('\'');
                envData.Light = result[3];
                envData.Proximity = result[7];

                if (_shouldShowTelemetryOutput)
                {
                    Console.WriteLine(new string('*', 80));
                    Console.WriteLine("* Telemetry Data: ");
                    Console.WriteLine(envData);
                    Console.WriteLine(new string('*', 80));
                }

                var telemetryObject = new BME280PlusLTR559(envData.Temperature, envData.Pressure,
                                                            envData.Humidity, envData.Altitude,
                                                            envData.Light, envData.Proximity);

                //telemetry object has the full output:
                var telemetryMessage = telemetryObject.ToJson();
                //create the message to send to the hub
                var msg = new Message(Encoding.ASCII.GetBytes(telemetryMessage));
                //send the telemetry to azure
                await _deviceClient.SendEventAsync(msg);

                //output result
                Console.WriteLine($"Telemetry sent {DateTime.Now.ToShortTimeString()}");
                Thread.Sleep(500);
            }

            Console.WriteLine("All telemetry read");
        }

        private static void BuildOptions()
        {
            _configuration = ConfigurationBuilderSingleton.ConfigurationRoot;
        }
    }
}
