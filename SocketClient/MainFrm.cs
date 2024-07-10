using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SocketClient
{
    public partial class MainFrm : Form
    {
        public Socket ClientSocket { get; set; }
        public MainFrm()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            //客户端连接服务器端
            //1 创建Socket对象
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            ClientSocket = socket;
            //2 连接服务器端
            try
            {
                socket.Connect(IPAddress.Parse(txtIP.Text), int.Parse(txtPort.Text));
            }
            catch (Exception ex)
            {
                MessageBox.Show("败了，重新连接");
                /*Thread.Sleep(500);
                btnConnect_Click(this, e);*/
                return;
            }
            //3 发送消息,接收消息
            Thread thread = new Thread(new ParameterizedThreadStart(ReceiveData));
            thread.IsBackground = true;
            thread.Start(ClientSocket);
        }
        public void AppendTextToTxtLog(string txt)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(s =>
                {
                    this.txtLog.Text = string.Format("{0}\r\n{1}", s, txtLog.Text);
                }), txt);
            }
            else
            {
                this.txtLog.Text = string.Format("{0}\r\n{1}", txt, txtLog.Text);
            }
        }
        # region 客户端接收数据
        public void ReceiveData(Object socket)
        {
            var proxSocket = socket as Socket;
            byte[] data = new byte[1024 * 1024];
            while (true)
            {
                int len = 0;
                try
                {
                    len = proxSocket.Receive(data, 0, data.Length, SocketFlags.None);
                }
                catch (Exception ex)
                {
                    //异常退出
                    AppendTextToTxtLog(string.Format("接收到服务器端：{0}非正常退出", proxSocket.RemoteEndPoint.ToString()));
                    StopConnect();//关闭连接
                    return;
                }
                if (len <= 0)
                {
                    //客户端正常退出
                    AppendTextToTxtLog(string.Format("服务器端：{0}正常退出", proxSocket.RemoteEndPoint.ToString()));
                    StopConnect();  //停止连接
                    return; //终结当前接收客户端数据的异步线程
                }
                //把接收到的数据放到文本框上去
                //接收的数据中第一个字节如果是1，那么是字符串；2是闪屏；3是文件
                #region 接收的是字符串
                if (data[0] == 1)
                {
                    string strMsg = ProcessReceiveString(data);
                    AppendTextToTxtLog(string.Format("接收到服务器端：{0}的消息是：{1}",
                    proxSocket.RemoteEndPoint.ToString(), strMsg));
                }
                #endregion
                #region 接收的是闪屏
                else if (data[0] == 2)
                {
                    Shake();
                }
                #endregion
                #region 接收的是文件
                else if (data[0] == 3)
                {
                    ProcessReceiveFile(data, len);
                }
                #endregion
            }
        }
        #endregion
        #region 处理接受到的文件
        public void ProcessReceiveFile(byte[] data, int len)
        {
            using(SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.DefaultExt = "txt";
                sfd.Filter = "文本文件(*.txt)|*.txt|所有文件(*.*)|*.*";
                if(sfd.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                byte[] fileData = new byte[len - 1];
                Buffer.BlockCopy(data, 1, fileData, 0, len - 1);
                File.WriteAllBytes(sfd.FileName, fileData);
            }
        }
        #endregion
        #region 抖动一下
        public void Shake()
        {
            //把窗体最原始的坐标记住
            Point oldLocation = this.Location;
            Random r = new Random();
            for(int i = 0; i < 50; i++)
            {
                this.Location = new Point(r.Next(oldLocation.X - 5, oldLocation.X + 5),
                    r.Next(oldLocation.Y - 5, oldLocation.Y + 5)
                    );
                Thread.Sleep(50);
                this.Location = oldLocation;
            }
        }
        #endregion
        #region 处理接收到的字符串
        public string ProcessReceiveString(byte[] data)
        {
            //把实际的字符串拿到
            string str = Encoding.Default.GetString(data, 1, data.Length - 1);
            return str;
        }
        #endregion
        private void StopConnect()
        {
            try
            {
                if (ClientSocket.Connected)
                {
                    ClientSocket.Shutdown(SocketShutdown.Both);
                    ClientSocket.Close(100);
                }
            }
            catch(Exception ex)
            {

            }
        }

        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            if (ClientSocket.Connected)
            {
                byte[] data = Encoding.Default.GetBytes(txtMsg.Text);
                ClientSocket.Send(data, 0, data.Length, SocketFlags.None);
            }
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            //判断是否已连接，如果连接那么就关闭连接
            StopConnect();

        }
    }
}
