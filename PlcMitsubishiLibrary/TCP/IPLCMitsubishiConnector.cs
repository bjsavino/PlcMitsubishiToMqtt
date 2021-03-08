using System.Threading.Tasks;

namespace PlcMitsubishiLibrary.TCP
{
    public interface IPLCMitsubishiConnector
    {
        void Connect();
        void Disconnect();
        void Dispose();
        int GetMemoryValue(PlcMemory memory);
        Task<int> GetMemoryValueAsync(PlcMemory memory);
        void SetMemoryValue(PlcMemory memory, int value);

        public bool IsConnected { get; }
    }
}