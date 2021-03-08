using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.Configuration;
using PlcMitsubishiLibrary;
using PlcMitsubishiLibrary.MQTT;
using PlcMitsubishiLibrary.TCP;
using PlcMitsubishiLibrary.Utils;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using MQTTnet.Client.Disconnecting;
using Newtonsoft.Json;

namespace plcMistubishiConsole
{
    class Program
    {
        private static PlcMqttConnection _mqttmaganer;
        private static PLCMitsubishiManager _plcMonitoring;
        private static readonly object _consoleLock = new object();
        private static AppSettings _settings;
        private static readonly string _appTitle = "PLCMitsubishi to MQTT";

        static void Main(string[] args)
       {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                var options = new Options();
                Parser.Default.ParseArguments<Options>(args)
                    .WithParsed(o =>
                    {

                        Log.Information("Loading Settings on {path}...", o.ConfigFile);
                        _settings = AppSettings.LoadFromPath(o.ConfigFile);
                        Log.Information("Settings Loaded: {@settings}", _settings);
                        Console.WriteLine();
                        UpdateTitle();
                        RunProgram();
                    }
                    )
                    .WithNotParsed(o =>
                    {
                        Log.Logger.Error("Failure to read args from commandline - Args:{arg}", args);
                        Log.CloseAndFlush();
                    }
                    );
            }
            catch (JsonReaderException jsonEx)
            {
                Log.Error(jsonEx, "Failure to load configuration File");
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failure on start program");
            }
            finally
            {
                Log.CloseAndFlush();
            }

        }

        private static void UpdateTitle()
        {
            Console.Title = $"{_appTitle}  -  PLC Connection:{_plcMonitoring?.PlcIsConnected} | Mqtt Connection:{_mqttmaganer?.IsConnected}";
        }

        private static async void RunProgram()
        {
            try
            {

                var serilogPlcMqtt = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PlcMqttConnection>();
                _mqttmaganer = new PlcMqttConnection(_settings, serilogPlcMqtt); ;
                _mqttmaganer.OnSetCommandReceived += Mqttmaganer_OnSetCommandReceived;
                _mqttmaganer.OnMqttClientConnected += Mqttmaganer_OnMqttClientConnected;
                _mqttmaganer.OnMqttClientDisconnected += Mqttmaganer_OnMqttClientDisconnected;
                Log.Information("Connecting to Mqtt using {ip}:{port}", _settings.MqttBroker_IPAddress, _settings.MqttBroker_Port);
                await _mqttmaganer.ConnectAsync();


                var serilogPlcConnector = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PLCMitsubishiConnector>();
                PLCMitsubishiConnector plc = new PLCMitsubishiConnector(_settings, serilogPlcConnector);

                var serilogPlcManager = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PLCMitsubishiManager>();
                _plcMonitoring = new PLCMitsubishiManager(plc, serilogPlcManager);
                _plcMonitoring.OnMemoryChangeValue += PlcMonitoring_OnMemoryChangeValue;
                _plcMonitoring.OnConnectedToPlcDevice += PlcMonitoring_OnConnectedToPlcDevice;
                _plcMonitoring.OnDisconnectedFromPlcDevice += PlcMonitoring_OnDisconnectedFromPlcDevice;
                _plcMonitoring.AddMemoriesFromSettings(_settings);

                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.S)
                    {
                        lock (_consoleLock)
                        {
                            PrintStatus();
                            continue;
                        }
                    }
                    if (key.Key == ConsoleKey.C)
                    {
                        lock (_consoleLock)
                        {
                            Console.Clear();
                        }
                    }
                }

            }
            catch (Exception ex)
            {

                Log.Logger.Fatal(ex, "Failure during program running");
            }
        }

        private static void PlcMonitoring_OnDisconnectedFromPlcDevice(object sender, EventArgs e)
        {
            UpdateTitle();
            _mqttmaganer.SetStatus("offline");
        }

        private static void PlcMonitoring_OnConnectedToPlcDevice(object sender, EventArgs e)
        {
            UpdateTitle();
            _mqttmaganer.SetStatus("online");
            lock (_consoleLock)
            {
                PrintStatus();
            }
        }

        private static void Mqttmaganer_OnMqttClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            lock (_consoleLock)
            {
                PrintStatus();
            }
            UpdateTitle();
        }

        private static void PrintStatus()
        {
            Console.WriteLine();
            Console.WriteLine("********************** Status **********************");
            Console.WriteLine($"{" Device",-23}{"Status",-8}{"IpAddress",-15}{"Port",-7}");
            Console.WriteLine($"{" Connection to PLC:",23}{_plcMonitoring.PlcIsConnected,-8}{_settings.PlcDevice_IpAddress,-15}{_settings.PlcDevice_Port,-7}");
            Console.WriteLine($"{" Connection to MQTT:",-23}{_mqttmaganer.IsConnected,-8}{_settings.MqttBroker_IPAddress,-15}{_settings.MqttBroker_Port,-7}");
            Console.WriteLine("****************************************************");
            Console.WriteLine();
        }

        private static void Mqttmaganer_OnMqttClientConnected(object sender, EventArgs e)
        {
            Log.Information("Staring Connection to PLC using {ip}:{port}", _settings.PlcDevice_IpAddress, _settings.PlcDevice_Port);
            _plcMonitoring.Start();

            lock (_consoleLock)
            {
                PrintStatus();
            }
            UpdateTitle();
        }

        private static void Mqttmaganer_OnSetCommandReceived(object sender, PlcMemory e)
        {
            if (!_plcMonitoring.PlcIsConnected)
            {
                Log.Logger.Warning("Cannot Set {mem} to {value} because PLC is not connected", e.FullAddress, e.Value);
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            _plcMonitoring.SetMemoryValue(e);
            stopwatch.Stop();
            Log.Information("Setted: {mem}->{value} [ {time}ms ]", e.FullAddress, e.Value, stopwatch.ElapsedMilliseconds);



        }

        private static async void PlcMonitoring_OnMemoryChangeValue(object sender, PlcMemory e)
        {
            if (_mqttmaganer.IsConnected == false)
            {
                Log.Warning("*MqttClient is not connected.");
                return;
            }
            await _mqttmaganer.UpdateMemoryValue(e);
        }
    }
}
