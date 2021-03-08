using System;
using System.Collections.Generic;
using System.Text;

namespace PlcMitsubishiLibrary.Utils
{
    public class MqttConnectionConfiguration
    {
        public string IPAddress { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string PlcName { get; set; }
    }
}
