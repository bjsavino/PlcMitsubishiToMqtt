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

namespace plcMistubishiConsole
{
    class Program
    {
        static PlcMqttConnection mqttmaganer;
        static PLCMitsubishiManager plcMonitoring;
        static object consoleLock = new object();
        static AppSettings _settings;
        static string appTitle = "PLCMitsubishi to MQTT";

#pragma warning disable CS1998 // Este método assíncrono não possui operadores 'await' e será executado de modo síncrono. É recomendável o uso do operador 'await' para aguardar chamadas à API desbloqueadas ou do operador 'await Task.Run(...)' para realizar um trabalho associado à CPU em um thread em segundo plano.
        static async Task Main(string[] args)
#pragma warning restore CS1998 // Este método assíncrono não possui operadores 'await' e será executado de modo síncrono. É recomendável o uso do operador 'await' para aguardar chamadas à API desbloqueadas ou do operador 'await Task.Run(...)' para realizar um trabalho associado à CPU em um thread em segundo plano.
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();


            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            var options = new Options();
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    try
                    {
                        Log.Information("Loading Settings...");
                        _settings = AppSettings.LoadFromConfig(o.ConfigFile);
                        Log.Information("Settings Loaded", o);
                        Console.WriteLine();
                        UpdateTitle();
                        RunProgram();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failure to load configuration File");
                        Log.CloseAndFlush();
                    }
                    



                }
                )
                .WithNotParsed(o =>
                {
                    Log.Logger.Error("Failure to read args from commandline", o);
                    Log.CloseAndFlush();
                }
                );

        }

        private static void UpdateTitle()
        {
            Console.Title = $"{appTitle}  -  PLC Connection:{plcMonitoring?.PlcIsConnected} | Mqtt Connection:{mqttmaganer?.IsConnected}";
        }

        private static async void RunProgram()
        {
            try
            {

                var serilogPlcMqtt = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PlcMqttConnection>();
                mqttmaganer = new PlcMqttConnection(_settings, serilogPlcMqtt); ;
                mqttmaganer.OnSetCommandReceived += Mqttmaganer_OnSetCommandReceived;
                mqttmaganer.OnMqttClientConnected += Mqttmaganer_OnMqttClientConnected;
                mqttmaganer.OnMqttClientDisconnected += Mqttmaganer_OnMqttClientDisconnected;
                Log.Information("Connecting to Mqtt using {ip}:{port}", _settings.MqttBroker_IPAddress, _settings.MqttBroker_Port);
                await mqttmaganer.ConnectAsync();


                var serilogPlcConnector = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PLCMitsubishiConnector>();
                PLCMitsubishiConnector plc = new PLCMitsubishiConnector(_settings, serilogPlcConnector);

                var serilogPlcManager = new Microsoft.Extensions.Logging.LoggerFactory().AddSerilog(Log.Logger).CreateLogger<PLCMitsubishiManager>();
                plcMonitoring = new PLCMitsubishiManager(plc, serilogPlcManager);
                plcMonitoring.OnMemoryChangeValue += PlcMonitoring_OnMemoryChangeValue;
                plcMonitoring.OnConnectedToPlcDevice += PlcMonitoring_OnConnectedToPlcDevice;
                plcMonitoring.OnDisconnectedFromPlcDevice += PlcMonitoring_OnDisconnectedFromPlcDevice;
                plcMonitoring.AddMemoriesFromSettings(_settings);



                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.S)
                    {
                        lock (consoleLock)
                        {
                            PrintStatus();
                            continue;
                        }
                    }
                    if (key.Key == ConsoleKey.C)
                    {
                        lock (consoleLock)
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

        }

        private static void PlcMonitoring_OnConnectedToPlcDevice(object sender, EventArgs e)
        {
            UpdateTitle();
            lock (consoleLock)
            {
                PrintStatus();
            }
        }

        private static void Mqttmaganer_OnMqttClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            lock (consoleLock)
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
            Console.WriteLine($"{" Connection to PLC:",23}{plcMonitoring.PlcIsConnected,-8}{_settings.PlcDevice_IpAddress,-15}{_settings.PlcDevice_Port,-7}");
            Console.WriteLine($"{" Connection to MQTT:",-23}{mqttmaganer.IsConnected,-8}{_settings.MqttBroker_IPAddress,-15}{_settings.MqttBroker_Port,-7}");
            Console.WriteLine("****************************************************");
            Console.WriteLine();
        }

        private static void Mqttmaganer_OnMqttClientConnected(object sender, EventArgs e)
        {
            Log.Information("Staring Connection to PLC using {ip}:{port}", _settings.PlcDevice_IpAddress, _settings.PlcDevice_Port);
            plcMonitoring.Start();

            lock (consoleLock)
            {
                PrintStatus();
            }
            UpdateTitle();
        }

        private static void Mqttmaganer_OnSetCommandReceived(object sender, PlcMemory e)
        {
            if (!plcMonitoring.PlcIsConnected)
            {
                Log.Logger.Warning("Cannot Set {mem} to {value} because PLC is not connected", e.FullAddress, e.Value);   
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            plcMonitoring.SetMemoryValue(e);
            stopwatch.Stop();
            Log.Information("Setting: {mem}->{value} [ {time}ms ]", e.FullAddress, e.Value, stopwatch.ElapsedMilliseconds);



        }

        private static async void PlcMonitoring_OnMemoryChangeValue(object sender, PlcMemory e)
        {
            if (mqttmaganer.IsConnected == false)
            {
                Log.Warning("*MqttClient is not connected.");
                return;
            }
            await mqttmaganer.UpdateMemoryValue(e);
        }
    }
}
