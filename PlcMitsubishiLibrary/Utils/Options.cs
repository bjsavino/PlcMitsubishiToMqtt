using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlcMitsubishiLibrary.Utils
{
    public class Options
    {
        [Option("config", Required = true, HelpText = "Input the configuration file to procced")]
        public string ConfigFile { get; set; }

    }
}
