using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlcMitsubishiLibrary.Utils
{
    public class AppSettings
    {
        public string MqttBroker_IPAddress { get; set; }
        public int MqttBroker_Port { get; set; }
        public string MqttBroker_User { get; set; }
        public string MqttBroker_Password { get; set; }
        public string PlcName { get; set; }
        public string PlcDevice_IpAddress { get; set; }
        public int PlcDevice_Port { get; set; }
        public List<string> MemoriesToMonitoring { get; set; }
        //private List<PlcMemory> MemoriesToMonitoring { get; set; }

        public static AppSettings LoadFromConfig(string filepath)
        {
            
            string contentFile = File.ReadAllText(filepath);
            var appSettings = JsonConvert.DeserializeObject<AppSettings>(contentFile);
         //   json
          //  var appSettings = new AppSettings();
            return appSettings;
        }



    }
}
