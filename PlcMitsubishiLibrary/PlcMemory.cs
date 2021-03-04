using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PlcMitsubishiLibrary
{
    public class PlcMemory
    {
        public string FullAddress { get; set; }
        public int Value { get; set; }

        public PlcMemory(string fullAddress) 
        {
            if (!IsValidAddress(fullAddress)) throw new ArgumentException($"Wrong {nameof(fullAddress)} memory to define {nameof(PlcMemory)}");
            FullAddress = fullAddress;
            Value = 0;
        }
        public PlcMemory(string fullAddress, int value):this(fullAddress)
        {
           Value = value;
        }

        public static bool IsValidAddress(string fullAddress)
        {
            Regex regex = new Regex(@"[DM]\d+\b");
            return regex.Match(fullAddress).Success;
        }
        public string GetMemoryType()
        {
            return FullAddress[0].ToString();
        }
        public string GetMemoryAddress()
        {
            return FullAddress[1..].ToString().PadLeft(6, '0');
        }
        public override string ToString()
        {
            return $"{FullAddress}->{Value}";
        }
    }
}
