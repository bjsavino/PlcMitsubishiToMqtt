using Microsoft.Extensions.Logging;
using PlcMitsubishiLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcMitsubishiLibrary.TCP
{
    public class PLCMitsubishiManager
    {
        private readonly IPLCMitsubishiConnector _plcConnector;
        private readonly ILogger<PLCMitsubishiManager> _logger;
        private List<PlcMemory> _memoriesToMonitoring;

        private CancellationTokenSource _tokenSource = new CancellationTokenSource();


        public event EventHandler<PlcMemory> OnMemoryChangeValue;
        public event EventHandler OnConnectedToPlcDevice;
        public event EventHandler OnDisconnectedFromPlcDevice;

        public bool PlcIsConnected { get => _plcConnector.IsConnected; }
        public PLCMitsubishiManager(IPLCMitsubishiConnector plcConnector, ILogger<PLCMitsubishiManager> logger)
        {
            _plcConnector = plcConnector;
            _logger = logger;
            _memoriesToMonitoring = new List<PlcMemory>();
        }

        public void Start()
        {

            _plcConnector.Connect();
            OnConnectedToPlcDevice?.Invoke(this, null);
            Task.Run(MonitoringPlc, _tokenSource.Token);
        }
        public void Stop()
        {
            _tokenSource.Cancel();
            _plcConnector.Disconnect();
            OnConnectedToPlcDevice?.Invoke(this, null);
        }

        public void AddMemoryToMonitoring(PlcMemory memory)
        {
            if (_memoriesToMonitoring.FirstOrDefault(m => m.FullAddress == memory.FullAddress) is null)
            {
                _memoriesToMonitoring.Add(memory);
            }
        }

        public void AddMemoriesFromSettings(AppSettings appSettings)
        {

            foreach (var memory in appSettings.MemoriesToMonitoring)
            {
                try
                {
                    var plcMemory = new PlcMemory(memory);
                    AddMemoryToMonitoring(plcMemory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failure to add Memory from Settings");
                }
            }

        }

        public List<PlcMemory> GetMonitoringMemories()
        {
            return _memoriesToMonitoring;
        }
        public void SetMemoryValue(PlcMemory plcMemory)
        {
            var memoryFromList = _memoriesToMonitoring.SingleOrDefault(m => m.FullAddress == plcMemory.FullAddress);
            if (memoryFromList is null)
                AddMemoryToMonitoring(plcMemory);

            //_memoriesToMonitoring[_memoriesToMonitoring.IndexOf(memoryFromList)] = plcMemory;
            memoryFromList = plcMemory;
            _plcConnector.SetMemoryValue(plcMemory, plcMemory.Value);
        }

        private void MonitoringPlc()
        {
            bool wasReadFirstTime = false;
            while (!_tokenSource.IsCancellationRequested)
            {
                if (!_plcConnector.IsConnected)
                {
                    _plcConnector.Connect();
                    OnConnectedToPlcDevice?.Invoke(this, null);
                }

                try
                {
                    foreach (var memory in _memoriesToMonitoring)
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        var newValue = _plcConnector.GetMemoryValue(memory);
                        if (memory.Value != newValue || wasReadFirstTime == false)
                        {
                            memory.Value = newValue;
                            stopwatch.Stop();
                            _logger.LogInformation("Changed: {mem}->{value} [ {time}ms ]", memory.FullAddress, memory.Value, stopwatch.ElapsedMilliseconds);
                            OnMemoryChangeValue?.Invoke(this, memory);
                        }
                    }
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex, "Failure during monitoring PLC Device");
                    _plcConnector.Disconnect();
                    OnDisconnectedFromPlcDevice?.Invoke(this, null);

                }
                wasReadFirstTime = true;
            }

        }
    }
}
