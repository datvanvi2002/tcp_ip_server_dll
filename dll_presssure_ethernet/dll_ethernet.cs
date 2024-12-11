using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SimpleTCP;


//dll write on 12/2024
/*Stable version code
 Function:
1. Connect to TCP client with {IP, Port} - Network Layer
2. Disconnect to TCP client
3. Check port open - Network Layer 
4. Check client was connected with LAN port - Physical layer
5. Sent message
6. Processing data rev
7. Data rev
8. Communication with MCU with TCP/IP client - RS232 - UART
 */
namespace dll_presssure_ethernet
{
    public class dll_ethernet
    {
        public SimpleTcpServer _server;
        public string DataSetpoint = "";
        public string PressureData1 = "";
        public string PressureData2 = "";
        public bool? status = null;

        public event Action<string> DataReceived;
        private bool flag_done_step = false;
        private bool flag_check_step = false;

        //Events need to connect and disconnect
        public event EventHandler<TcpClient> ClientConnected;
        public event EventHandler<TcpClient> ClientDisconnected;
        //Creat list to save clients was connected
        private List<TcpClient> connectedClients = new List<TcpClient>();
        public bool OpenEthernet(string ipAddress, int port) //default ip 192.168.0.201 and port 8234
        {
            try
            {
                if (!IsEthernetOpen())
                {
                    _server = new SimpleTcpServer();
                    _server.Delimiter = 0x13; //."Carriage Return ASCII code
                    _server.StringEncoder = Encoding.UTF8;
                    _server.DataReceived += OnDataReceived;

                    // processing event when client connected
                    _server.ClientConnected += (sender, client) =>
                    {
                        connectedClients.Add(client); //Add client in list
                        Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                    };
                    // processing event when client disconnected
                    _server.ClientDisconnected += (sender, client) =>
                    {
                        connectedClients.Remove(client); // Delete client from list
                        Console.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
                    };

                    System.Net.IPAddress ip;
                    if (System.Net.IPAddress.TryParse(ipAddress, out ip))
                    {
                        _server.Start(ip, port);
                        Console.WriteLine($"Server started on {ipAddress}:{port}");
                    }
                    else
                    {
                        throw new ArgumentException("Invalid IP address format.");
                    }
                    return true;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception)
            {
                //Console.WriteLine("Error opening port: " + ex.Message);
                return false;
            }
        }
        private List<TcpListener> listeners = new List<TcpListener>();

        public void StopEthernet()
        {
            if (_server != null)
            {
                _server.Stop();
                foreach (var client in connectedClients)
                {
                    client.Close(); // Disconnect client
                }
                connectedClients.Clear();
                _server = null;
            }
        }
        public bool IsEthernetOpen()
        {
            /*network layer*/
            if (_server != null)
            {
                //check server start and client was connected
                return _server.IsStarted;
            }
            return false;
        }
        public int num_client = 0;
        public bool IsClientConnected()
        {
            /* Check Physical layer*/
            // check client was connected
            num_client = connectedClients.Count;
            if (num_client > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //----------------------------------------------------------------------------------//
        public void SendMessage(string message)
        {
            if (_server.IsStarted && _server != null)
            {
                _server.Broadcast(message);
            }
            else
            {
                throw new InvalidOperationException("Server is not started.");
            }
        }

        public void OnDataReceived(object sender, SimpleTCP.Message e)
        {
            if (_server.IsStarted && _server != null)
            {
                /* data rev no process */
                PressureData1 += e.MessageString.TrimEnd((char)0x13);

                /* limit buffer */
                int maxLength = 1000; 
                if (PressureData1.Length >= maxLength)
                {
                    PressureData1 = PressureData1.Substring(PressureData1.Length - maxLength);
                }

                /*process data rev
                 PressureData2 = latest data + \n */
                if (PressureData1.Contains("\n"))
                {
                    string[] data_saving = PressureData1.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    PressureData2 = data_saving[data_saving.Length - 1];
                }
                else
                {
                    PressureData2 = "FAILED";
                }

                /*Check done step setup*/
                if (PressureData2.StartsWith("DONE_"))
                {
                    flag_done_step = true;
                }

                /*Check  need step */
                if (PressureData2.StartsWith("true_"))
                {
                    flag_check_step = true;
                }
                else if (PressureData2.StartsWith("false_"))
                {
                    flag_check_step = false;
                }
            }
        }
        public string GetData()
        {
            return PressureData2;
        }
        //----------------------------------------------------------------------------------//
        /**/

        public void ReadFileTXT(string file)
        {
            DataSetpoint = File.ReadAllText(file);
        }

        public static float[] ConvertListToArray(List<float> list)
        {
            float[] array = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                array[i] = (float)list[i];
            }
            return array;
        }

        public string SendSPToRenesas()
        {
            //check file format DataSetpoint: [xx.xx,yy.yy,...,zz.zz]
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(DataSetpoint + "\n");
            }
            return DataSetpoint; //?? why return
        }
        public string Stop()
        {
            string STOP = "STOP";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(STOP + "\n");
            }
            return STOP;
        }
        public string Play()
        {
            string START = "START";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(START + "\n");
            }
            return START;
        }

        public string Pause()
        {
            string PAUSE = "PAUSE";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(PAUSE + "\n");
            }
            return PAUSE;
        }
        public string Continue()
        {
            string CONTINUE = "CONTINUE";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(CONTINUE + "\n");
            }
            return CONTINUE;
        }
        /*
         private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            // Unused method; consider removing or implementing functionality
        }
         */

        char statusServor1;
        public char Status()
        {
            if (string.IsNullOrEmpty(PressureData2))
            {
                throw new ArgumentException("Input string cannot be null or empty.");
            }
            statusServor1 = PressureData2[PressureData2.Length - 2];
            return statusServor1;
        }

        // for control pump and valve
        public bool CheckStep(int stepID)
        {
            flag_check_step = false;
            string check = $"CHECK_{stepID}";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(check + "\n");
            }
            Thread.Sleep(500);
            return flag_check_step;

        }
        public void StartStep(int stepID)
        {
            string text = $"SETUP_{stepID}";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(text + "\n");
            }
            flag_done_step = false;
        }
        public void StopStep(int stepID)
        {
            string text = $"STOP_{stepID}";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(text + "\n");
            }
        }
        public void PauseStep(int stepID)
        {
            string text = $"PAUSE_{stepID}";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(text + "\n");
            }
        }
        public void ContinueStep(int stepID)
        {
            string text = $"CONTINUE_{stepID}";
            if (_server != null && _server.IsStarted)
            {
                _server.Broadcast(text + "\n");
            }
        }
        public bool CheckDoneStep(int stepID)
        {
            return flag_done_step;
        }
    }

}
