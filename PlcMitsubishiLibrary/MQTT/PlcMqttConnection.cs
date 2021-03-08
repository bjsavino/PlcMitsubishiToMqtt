using MQTTnet.Client.Options;
using MQTTnet.Client.Disconnecting;

using MQTTnet;
using System;
using System.Collections.Generic;
using System.Text;
using MQTTnet.Extensions.ManagedClient;
using System.Threading.Tasks;
using System.Linq;
using PlcMitsubishiLibrary.Utils;
using Microsoft.Extensions.Logging;
using MQTTnet.Client.Connecting;

namespace PlcMitsubishiLibrary.MQTT
{
    public class PlcMqttConnection
    {
        private readonly IManagedMqttClient _client;
        private readonly string _mqttURI;
        private readonly string _mqttUser;
        private readonly string _mqttPassword;
        private readonly int _mqttPort;
        private readonly string _plcName;
        private readonly ILogger<PlcMqttConnection> _logger;
        private const string _topicGroupName = "PLCMitsubishiMQTT";

        public event EventHandler<PlcMemory> OnSetCommandReceived;
        public event EventHandler OnMqttClientConnected;
        public event EventHandler<MqttClientDisconnectedEventArgs> OnMqttClientDisconnected;
        public bool IsConnected { get => _client.IsConnected; }

        public PlcMqttConnection(AppSettings appSettings, ILogger<PlcMqttConnection> logger)
        {
            if (string.IsNullOrEmpty(appSettings.PlcName))
            {
                throw new ArgumentNullException("You need define a PLCName on config file");
            }

            _mqttURI = appSettings.MqttBroker_IPAddress;
            _mqttUser = appSettings.MqttBroker_User;
            _mqttPassword = appSettings.MqttBroker_Password;
            _mqttPort = appSettings.MqttBroker_Port;
            _plcName = appSettings.PlcName;
            _client = new MqttFactory().CreateManagedMqttClient();
            _logger = logger;
        }
        public async Task ConnectAsync()
        {
            try
            {
                string clientId = _plcName + "-" + Guid.NewGuid().ToString();
                bool mqttSecure = false;
                var messageBuilder = new MqttClientOptionsBuilder()
                  .WithClientId(clientId)
                  .WithCredentials(_mqttUser, _mqttPassword)
                  .WithTcpServer(_mqttURI, _mqttPort)
                  .WithCleanSession()
                  .WithWillMessage(new MqttApplicationMessageBuilder().WithPayload("offline").WithTopic($"{_topicGroupName}/{_plcName}/Status").WithRetainFlag(true).Build());

                var options = mqttSecure
                  ? messageBuilder
                    .WithTls()
                    .Build()
                  : messageBuilder
                    .Build();

                //MQTTnet.Client.MqttClient client = new MQTTnet.Client.MqttClient()
                var managedOptions = new ManagedMqttClientOptionsBuilder()
                  .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                  .WithClientOptions(options)
                  .Build();

                await _client.StartAsync(managedOptions);

                _client.UseApplicationMessageReceivedHandler(e => OnMessageReceived(e));
                _client.UseConnectedHandler(e => OnConnected(e));
                _client.UseDisconnectedHandler(e => OnDisconnected(e));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to Connect to MQTT Broker");
            }
        }

        private async void OnConnected(MqttClientConnectedEventArgs e)
        {
            await PublishAsync($"{_topicGroupName}/{_plcName}/Status", "online", true);
            await SubscribeAsync($"{_topicGroupName}/{_plcName}/SET/+");
            _logger.LogInformation("Mqtt Client Connected");
            OnMqttClientConnected?.Invoke(this, e);
        }

        private void OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            _logger.LogWarning("Mqtt Client was Disconnected - Reason:{reason} | Autentication:{auth}", e.Reason, e.AuthenticateResult);
            OnMqttClientDisconnected?.Invoke(this, e);
        }
        private void OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                string topic = e.ApplicationMessage.Topic;
                if (string.IsNullOrWhiteSpace(topic) == false)
                {
                    string memoryAddress = topic.Split('/').Last().ToUpper();
                    string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    int memoryValue = Convert.ToInt32(payload);
                    PlcMemory memoryToSet = new PlcMemory(memoryAddress, memoryValue);

                    OnSetCommandReceived?.Invoke(this, memoryToSet);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure OnMessageReceived");
            }
        }

        private async Task PublishAsync(string topic, string payload, bool retainFlag = true, int qos = 1)
        {
            await _client.PublishAsync(new MqttApplicationMessageBuilder()
              .WithTopic(topic)
              .WithPayload(payload)
              .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
              .WithRetainFlag(retainFlag)
              .Build());
        }

        private async Task SubscribeAsync(string topic, int qos = 1)
        {

            await _client.SubscribeAsync(new MqttTopicFilterBuilder()
              .WithTopic(topic)
              .WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)qos)
              .Build());
        }

        public async void SetStatus(string status)
        {
            await PublishAsync($"{_topicGroupName}/{_plcName}/Status", status, true);
        }

        public async Task UpdateMemoryValue(PlcMemory memory)
        {
            await PublishAsync($"{_topicGroupName}/{_plcName}/GET/{memory.FullAddress}", memory.Value.ToString(), true);
        }

    }
}
