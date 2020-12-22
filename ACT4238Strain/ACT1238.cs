using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DataAcquisition
{
    class SerialACT4238Config
    {
        public string Database = "Data Source = LongRui.db";
        public string ip;
        public int port;
        public string path;
        public int id;
        public string deviceType;

        public SerialACT4238Config(string _ip,int _port,string _path, int _id, string type)
        {
            this.ip = _ip;
            this.port = _port;
            this.path = _path;
            this.id = _id;
            this.deviceType = type;
        }
    }

    public class DataValue
    {
        public string SensorId { get; set; }
        public string TimeStamp { get; set; }
        public string ValueType { get; set; }
        public double Value { get; set; }
    }

    class ACT1238
    {
        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public event EventHandler<UpdateDataGridViewCellsEventArgs> UpdateDataGridView;
        private SerialACT4238Config config;
        private Dictionary<int, StrainChannel> strainChannels;
        private BackgroundWorker backgroundWorkerReceiveData;
        private UdpClient udpClient;
        private const int NumberOfChannels = 8;
        private string Tag;

        public ACT1238(SerialACT4238Config config, Dictionary<int, StrainChannel> channels)
        {
            this.Tag = config.ip + " : ";
            this.config = config;
            strainChannels = channels;

            backgroundWorkerReceiveData = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            backgroundWorkerReceiveData.DoWork += BackgroundWorkerReceiveData_DoWork;
        }

        protected virtual void OnUpdateDataGridView(UpdateDataGridViewCellsEventArgs e)
        {
            //EventHandler<UpdateDataGridViewCellsEventArgs> handler = UpdateDataGridView;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
            UpdateDataGridView?.Invoke(this, e);
        }

        public void Start()
        {
            udpClient = new UdpClient(config.port);
            backgroundWorkerReceiveData.RunWorkerAsync();
        }

        public void Stop()
        {
            backgroundWorkerReceiveData.CancelAsync();
            udpClient.Close();
        }

        private void BackgroundWorkerReceiveData_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgWorker = sender as BackgroundWorker;

            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                try
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    if (receiveBytes.Length != 44)
                    {
                        continue;
                    }
                    StringBuilder sb = new StringBuilder(1024);
                    sb.Append(stamp + ",");
                    int startIndex = 4;
                    for (int i = 0; i < NumberOfChannels; i++)
                    {
                        if (!strainChannels.ContainsKey(i + 1))
                        {
                            continue;
                        }
                        //digit
                        double digit = CalculateDigit(receiveBytes, startIndex, i);

                        //温度
                        double temperature = CalculateTempreture(receiveBytes, startIndex, i);
                        StrainChannel sc = strainChannels[i + 1];
                        double strain = sc.CalculateStrain(digit, temperature);

                        bool isSuccess = true;
                        if (digit < 0.001 || Math.Abs(temperature) < 0.001)
                        {
                            log.Warn(Tag + "Sensor Error: digit=" + digit.ToString() + "temperature=" + temperature.ToString());
                            isSuccess = false;
                        }
                        string strainVal = "";
                        string strainState = "";
                        if (!isSuccess)
                        {
                            strainVal = "频率或者温度为零无法计算";
                            strainState = "failed";
                        }
                        else
                        {
                            strainVal = strain.ToString();
                            strainState = "Success";
                        }
                        UpdateDataGridViewCellsEventArgs args = new UpdateDataGridViewCellsEventArgs();
                        args.index = sc.gridIndex;
                        args.digit = digit;
                        args.stamp = stamp;
                        args.temp = temperature;
                        args.strain = strainVal;
                        args.state = strainState;
                        OnUpdateDataGridView(args);

                        sb.Append(digit.ToString() + "," + temperature.ToString() + ",");
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sb.Append("\r\n");
                    AppendRecord(sb,this.config.ip);
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(Tag, ex);
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                if (bgWorker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
            }
        }
        
        /// <summary>
        /// 写记录
        /// </summary>
        /// <param name="str"></param>
        public void AppendRecord(StringBuilder sb, string folder)
        {
            string parent = Path.Combine(this.config.path, folder);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }

            string currentDate = DateTime.Now.ToString("yyyy-MM-dd") + ".csv";

            string pathString = Path.Combine(parent, currentDate);

            using (StreamWriter sw = new StreamWriter(pathString, true))
            {
                sw.Write(sb);
                sw.Close();
            }
        }
        double CalculateTempreture(byte[] buffer, int startIndex, int i)
        {
            byte sign = (byte)(buffer[startIndex + i * 5 + 3] & 0x80);
            byte higher = (byte)(buffer[startIndex + i * 5 + 3] & 0x7F);
            byte lower = buffer[startIndex + i * 5 + 4];
            double temp = (higher * 256 + lower) / 10.0;
            if (sign == 0x80)
            {
                temp = -temp;
            }
            return Math.Round(temp, 3);
        }

        double CalculateDigit(byte[] buffer, int startIndex, int i)
        {
            double strain = (buffer[startIndex + i * 5 + 0] * 256 * 256 + buffer[startIndex + i * 5 + 1] * 256 + buffer[startIndex + i * 5 + 2]) / 100.0;
            //频率转化为模数
            strain = strain * strain / 1000;
            return Math.Round(strain, 3);
        }
    }

    public class InsertDataGridViewCellEventArgs : EventArgs
    {
        public string desc { get; set; }
    }

    public class UpdateDataGridViewCellsEventArgs : EventArgs
    {
        public int index { get; set; }
        public string stamp { get; set; }
        public double digit { get; set; }
        public string strain { get; set; }
        public double temp { get; set; }
        public string state { get; set; }
    }

    class StrainChannel
    {
        public string SensorId;
        public double G;
        public double R0;
        public double K;
        public double T0;
        public double C;
        public double InitStrain;
        public double currentValue;
        public bool IsUpdated;
        public string description;
        public int gridIndex;

        public StrainChannel(string sensorId, double g, double r0, double k, double t0, double c, double initValue, string desc, int gridIndex)
        {
            this.SensorId = sensorId;
            this.gridIndex = gridIndex;
            this.G = g;
            this.R0 = r0;
            this.K = k;
            this.T0 = t0;
            this.C = c;
            this.InitStrain = initValue;
            this.currentValue = 0;
            this.IsUpdated = false;
            this.description = desc;
        }

        public double CalculateStrain(double frequency, double temperature)
        {
            currentValue = this.G * (frequency - this.R0) + this.K * (temperature - this.T0) + this.C - this.InitStrain;
            return Math.Round(currentValue, 3);
        }
    }
}
