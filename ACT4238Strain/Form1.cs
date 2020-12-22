using DataAcquisition;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Forms;

namespace ACT1238Strain
{
    public partial class Form1 : Form
    {
        //Here is the once-per-class call to initialize the log object
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// 设备列表
        /// </summary>
        private Dictionary<string, ACT1238> deviceList;
        private string database = "DataSource = LongRui.db";

        public Form1()
        {
            InitializeComponent();
            deviceList = new Dictionary<string, ACT1238>();
            buttonStopAcquisit.Enabled = false;
            LoadDevices();
        }

        private Dictionary<int, StrainChannel> LoadChannels(SerialACT4238Config config)
        {
            Dictionary<int, StrainChannel> strainChannels = new Dictionary<int, StrainChannel>();
            using (SQLiteConnection connection = new SQLiteConnection(config.Database))
            {
                connection.Open();
                string strainStatement = "select SensorId,ChannelNo,G,R0,K,T0,Constant,InitVal,Desc from StrainChannels where GroupNo ='" + config.id + "'";
                SQLiteCommand command = new SQLiteCommand(strainStatement, connection);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string sensorId = reader.GetString(0);
                        int channelNo = reader.GetInt32(1);
                        double g = reader.GetDouble(2);
                        double r0 = reader.GetDouble(3);
                        double k = reader.GetDouble(4);
                        double t0 = reader.GetDouble(5);
                        double constant = reader.GetDouble(6);
                        double initVal = reader.GetDouble(7);
                        string desc = reader.GetString(8);
                        int index = this.dataGridView1.Rows.Add();
                        this.dataGridView1.Rows[index].Cells[0].Value = desc;

                        StrainChannel channel = new StrainChannel(sensorId, g, r0, k, t0, constant, initVal, desc, index);
                        strainChannels.Add(channelNo, channel);
                    }
                    return strainChannels;
                }
            }
        }

        private void LoadDevices()
        {
            this.deviceList.Clear();
            using (SQLiteConnection connection = new SQLiteConnection(database))
            {
                connection.Open();
                string strainStatement = "select Ip,Port,DeviceId,Path,Type,Desc from SensorInfo";
                SQLiteCommand command2 = new SQLiteCommand(strainStatement, connection);
                using (SQLiteDataReader reader = command2.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string ip = reader.GetString(0);
                        int port = reader.GetInt32(1);
                        int deviceId = reader.GetInt32(2);
                        string path = reader.GetString(3);
                        string type = reader.GetString(4);
                        string description = reader.GetString(5);

                        string[] itemString = { description, ip, port.ToString(), deviceId.ToString(), type, path };
                        ListViewItem item = new ListViewItem(itemString);

                        listView1.Items.Add(item);

                        SerialACT4238Config config = new SerialACT4238Config(ip, port, path, deviceId, type);
                        Dictionary<int, StrainChannel> channels = LoadChannels(config);

                        ACT1238 device = null;

                        if (type == "ACT1238")
                        {
                            device = new ACT1238(config, channels);
                            device.UpdateDataGridView += new EventHandler<UpdateDataGridViewCellsEventArgs>(UpdateGridViewCell);
                        }
                        else { }

                        if (device != null)
                        {
                            this.deviceList.Add(deviceId.ToString(), device);
                        }
                    }
                }
            }
        }

        private void UpdateGridViewCell(object sender, UpdateDataGridViewCellsEventArgs e)
        {
            if (dataGridView1.InvokeRequired)
            {
                dataGridView1.BeginInvoke(new MethodInvoker(() => {
                    dataGridView1.Rows[e.index].Cells[1].Value = e.stamp;
                    dataGridView1.Rows[e.index].Cells[2].Value = e.digit;

                    dataGridView1.Rows[e.index].Cells[4].Value = e.temp;
                    dataGridView1.Rows[e.index].Cells[3].Value = e.strain;
                    dataGridView1.Rows[e.index].Cells[5].Value = e.state;
                    dataGridView1.CurrentCell = dataGridView1.Rows[e.index].Cells[0];
                }));
            }
            else
            {
                dataGridView1.Rows[e.index].Cells[1].Value = e.stamp;
                dataGridView1.Rows[e.index].Cells[2].Value = e.digit;

                dataGridView1.Rows[e.index].Cells[4].Value = e.temp;
                dataGridView1.Rows[e.index].Cells[3].Value = e.strain;
                dataGridView1.Rows[e.index].Cells[5].Value = e.state;
                dataGridView1.CurrentCell = dataGridView1.Rows[e.index].Cells[0];
            }
            
        }

        private void buttonStartAcquisit_Click(object sender, EventArgs e)
        {
            StartAcquisit();
        }

        private void buttonStopAcquisit_Click(object sender, EventArgs e)
        {
            StopAcquisit();
        }

        private void StartAcquisit()
        {
            buttonStartAcquisit.Enabled = false;
            buttonStopAcquisit.Enabled = true;
            foreach(var dv in deviceList.Values)
            {
                dv.Start();
            }
        }

        private void StopAcquisit()
        {
            buttonStartAcquisit.Enabled = true;
            buttonStopAcquisit.Enabled = false;
            foreach (var dv in deviceList.Values)
            {
                dv.Stop();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 注意判断关闭事件reason来源于窗体按钮，否则用菜单退出时无法退出!
            if (e.CloseReason == CloseReason.UserClosing)
            {
                //取消"关闭窗口"事件
                e.Cancel = true; // 取消关闭窗体 

                //使关闭时窗口向右下角缩小的效果
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
                return;
            }
        }

        

        private void RestoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.notifyIcon1.Visible = true;
            this.Show();
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要退出？", "系统提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                this.notifyIcon1.Visible = false;
                this.Close();
                this.Dispose();
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.Visible)
            {
                this.WindowState = FormWindowState.Minimized;
                this.notifyIcon1.Visible = true;
                this.Hide();
            }
            else
            {
                this.Visible = true;
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }
    }
}
