using SimpleTcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PlcMistubishi_FakeDevice
{
    public partial class FormMain : Form
    {
        List<PlcMemory> _plcData;
        TcpListener _tcpListener;
        SimpleTcpServer server;
        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            FillInitialData();
            timerUpdateUI.Interval = 100;
            timerUpdateUI.Start();
            timerUpdateUI.Tick += TimerUpdateUI_Tick;

        }

        private void TimerUpdateUI_Tick(object sender, EventArgs e)
        {
            bindingSourceMemories.DataSource = _plcData;
          //  bindingSourceMemories.ResetCurrentItem();
            dataGridView1.DataSource = bindingSourceMemories;
            dataGridView1.Update();
            this.Update();
        }

        private void FillInitialData()
        {
            _plcData = new List<PlcMemory>()
            {
                new PlcMemory("D100",10),
                new PlcMemory("D200",5),
                new PlcMemory("M2500",65535)
            };

            bindingSourceMemories.DataSource = _plcData;
            bindingSourceMemories.DataSourceChanged += BindingSourceMemories_DataSourceChanged;
            bindingSourceMemories.ListChanged += BindingSourceMemories_ListChanged;
            dataGridView1.DataSource = bindingSourceMemories;
        }

        private void BindingSourceMemories_ListChanged(object sender, ListChangedEventArgs e)
        {
            dataGridView1.Update();
        }

        private void BindingSourceMemories_DataSourceChanged(object sender, EventArgs e)
        {
            dataGridView1.Update();
        }


        private async Task InitServer()
        {
            server = new SimpleTcpServer(textBoxIpAddress.Text,Convert.ToInt32(textBoxPort.Text));

            server.Events.ClientConnected += ClientConnected;
            server.Events.ClientDisconnected += ClientDisconnected;
            server.Events.DataReceived += DataReceived;

            server.Start();
        }

        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            string dataString = Encoding.UTF8.GetString(e.Data).Replace("\0", "").Trim();
            if (dataString.StartsWith("500000FF03FF000018001004010000") && dataString.Length >= 42)
            {
                //get comand
                //@"500000FF03FF000018001004010000D*0001000002";
                var letter = dataString[30].ToString().ToUpper();
                var address = int.Parse(dataString[32..38]).ToString();
                string fullAddress = letter + address;

                var valueMemory = _plcData.FirstOrDefault(m => m.Memory == fullAddress);
                string msgWithValue = $"000000000000000000000_{valueMemory.Value.ToString("X").PadLeft(4, '0')}_000";

                server.Send(e.IpPort, msgWithValue);
                // await tcpClient.Client.SendAsync(Encoding.ASCII.GetBytes(msgWithValue), SocketFlags.None);

            }
            if (dataString.StartsWith("500000FF03FF000020001014010000") && dataString.Length == 50)
            {
                var letter = dataString[30].ToString().ToUpper();
                var address = int.Parse(dataString[32..38]).ToString();
                string fullAddress = letter + address;

                var value = Convert.ToInt32(dataString[42..46], 16);

                var memory = _plcData.FirstOrDefault(m => m.Memory == fullAddress);
                if (memory is null)
                {
                    _plcData.Add(new PlcMemory(fullAddress, value));
                }
                else
                {
                    memory.Value = value;
                }
                string msgWithValue = $"000000000000000000000_{value.ToString("X").PadLeft(4, '0')}_000";
                server.Send(e.IpPort, msgWithValue);
                // var a = await tcpClient.Client.SendAsync(Encoding.ASCII.GetBytes(msgWithValue), SocketFlags.None);

            }
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            // throw new NotImplementedException();
        }

        private void ClientConnected(object sender, ClientConnectedEventArgs e)
        {
            //   throw new NotImplementedException();
        }

        private async void buttonStartStop_Click(object sender, EventArgs e)
        {
            if (buttonStartStop.Text == "Start")
            {
                buttonStartStop.Text = "Stop";
                await Task.Run(() => InitServer());
            }
            else
            {
                buttonStartStop.Text = "Start";
                server.Stop();
                var clients = server.GetClients();
                foreach (var client in clients)
                {
                    server.DisconnectClient(client);
                }
                server.Dispose();
            }
        }
    }
}
