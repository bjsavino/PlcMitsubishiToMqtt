using Microsoft.Extensions.Logging;
using PlcMitsubishiLibrary.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlcMitsubishiLibrary.TCP
{
    public class PLCMitsubishiConnector : IDisposable, IPLCMitsubishiConnector
    {
        private TcpClient _tcpClient;
        private readonly string _iPAddress;
        private readonly int _port;
        private readonly ILogger<PLCMitsubishiConnector> _logger;
        private object lockConnection = new object();
        private object lockSend = new object();
        private bool tryingReconnection = false;

        public bool IsConnected => (_tcpClient.Client is null) ? false : _tcpClient.Client.Connected;

        public PLCMitsubishiConnector(AppSettings appSettings, ILogger<PLCMitsubishiConnector> logger)
        {
            _tcpClient = new TcpClient();
            _iPAddress = appSettings.PlcDevice_IpAddress;
            _port = appSettings.PlcDevice_Port;
            _logger = logger;
        }
        public void Connect()
        {
            while (_tcpClient.Connected == false)
            {
                lock (lockConnection)
                {
                    try
                    {
                        _tcpClient = new TcpClient();
                        _tcpClient.Connect(_iPAddress, _port);
                        _logger.LogInformation("Connection to PLC established");
                        tryingReconnection = false;

                    }
                    catch (SocketException ex)
                    {
                        int delaySeconds = 5;
                        if (tryingReconnection == false)
                        {
                            _logger.LogWarning(ex.Message);
                            tryingReconnection = true;
                        }
                        _logger.LogInformation("Try Reconnection again each {timereconection} seconds", 5);
                        Thread.Sleep(delaySeconds * 1000);

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Cannot connect to PLC");
                    }

                }
            }
        }
        public void Disconnect()
        {
            try
            {


                _logger.LogInformation("Disconnecting PLC Device");
                _tcpClient?.Client?.Close();
                _tcpClient?.Close();
                _logger.LogInformation("Disconnected from PLC Device");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to Disconnect the PLC Device");
            }
        }


        public int GetMemoryValue(PlcMemory memory)
        {

            string msg = "500000FF03FF000018001004010000" + memory.GetMemoryType() + "*" + memory.GetMemoryAddress() + "0002";
            string response = this.Send(msg);


            if (response.Length >= 30)
                return Convert.ToInt32(response.Substring(22, 4), 16);

            throw new Exception("Invalid data from PLC");
        }
        public void SetMemoryValue(PlcMemory memory, int value)
        {
            string hexvalue = value.ToString("X").PadLeft(4, '0');
            string msg = "500000FF03FF000020001014010000" + memory.GetMemoryType() + "*" + memory.GetMemoryAddress() + "0002" + hexvalue + "0000";
            var a = this.Send(msg);
        }

        private string Send(string message)
        {
            if (_tcpClient.Connected == false)
            {
                Connect();
            }
            string dataString;

            lock (lockSend)
            {

                byte[] data = Encoding.ASCII.GetBytes(message);

                _tcpClient.Client.Send(data, SocketFlags.None);

                byte[] receivedData = new byte[50];
                _tcpClient.Client.Receive(receivedData, SocketFlags.None);
                dataString = Encoding.ASCII.GetString(receivedData).Replace("\0", "").Trim();
            }

            return dataString;

        }
        public void Dispose()
        {
            if (_tcpClient is { }) _tcpClient.Close();
        }
    }
}
