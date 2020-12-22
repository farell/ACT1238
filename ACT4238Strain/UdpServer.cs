using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Timers;

namespace DataAcquisition
{
    class UdpServer
    {
        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected SerialPort comPort;
        private byte[] buffer;
        private string portName;
        private int baudrate;
        protected string stamp;

        public UdpServer(string portName,int baudrate)
        {
            comPort = new SerialPort(portName, baudrate, Parity.None, 8, StopBits.One);
            comPort.DataReceived += new SerialDataReceivedEventHandler(ComDataReceive);
            this.buffer = new byte[1024 * 4];
            this.portName = portName;
            this.baudrate = baudrate;
        }

        public void Start()
        {
            comPort.Open();
        }

        public void SendFrame(byte[] buffer)
        {
            if (comPort.IsOpen)
            {
                comPort.Write(buffer, 0, buffer.Length);
            }            
        }
        //Acquisit 每次在操作完成之后必须调用:base.Stop();
        public virtual void Acquisit(string stamp) { this.stamp = stamp; }
        public virtual string GetDeviceId() { return ""; }
        public virtual double GetResult() { return 0; }
        public virtual string GetObjectType() { return ""; }
        public virtual string GetResultString() { return ""; }
        public virtual void ProcessData(byte[] buffer, int length) {}
        public virtual bool GetAcquisiState() { return false; }

        public void Stop()
        {
            if (comPort.IsOpen)
            {
                comPort.Close();
            }
        }

        private void ComDataReceive(object sender, SerialDataReceivedEventArgs e)
        {
            //lock (sender)
            {
                //int bytesToRead = this.comPort.BytesToRead;
                int bytesRead = 0;
                try
                {
                    bytesRead = this.comPort.Read(this.buffer, 0, this.comPort.ReadBufferSize);
                    if (bytesRead > 0)
                    {
                        this.ProcessData(this.buffer, bytesRead);
                    }
                    //Stop();
                }
                catch (Exception ex)
                {
                    log.Error("串口接收错误", ex);
                    Stop();
                }

            }
        }
    }
}
