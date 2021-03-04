using System;
using System.Collections.Generic;
using System.Text;

namespace PlcMistubishi_FakeDevice
{
    public class PlcMemory
    {
        public PlcMemory(string memory, int value)
        {
            Memory = memory;
            Value = value;
        }

        public string Memory { get; set; }
        public int Value { get; set; }
    }
}
