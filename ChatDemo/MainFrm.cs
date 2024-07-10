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

namespace ChatDemo
{
    public partial class MainFrm : Form
    {
        List<Socket> ClientProxSocketList = new List<Socket>();
        public MainFrm()
        {
            InitializeComponent();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            //1 创建Socket对象
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //2 绑定端口ip
            socket.Bind(new IPEndPoint(IPAddress.Parse(txtIP.Text), int.Parse(txtPort.Text)));
            //3 开启侦听
            socket.Listen(10);    //等待连接的队列，连接等待队列的最大数目（取它和操作系统允许最大值的最大值）；同时来了100链接请求，
                                  //队列里面放10个等待连接的客户端，其他的返回错误信息
                                  //4 开始接受客户端的连接
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.AcceptClientConnect), socket);

        }
        public void AcceptClientConnect(Object socket)
        {
            var serverSocket = socket as Socket;
            this.AppendTextToTxtLog("服务器端开始接收客户端的连接。");
            while (true)
            {
                var proxSocket = serverSocket.Accept();
                this.AppendTextToTxtLog(string.Format("客户端：{0}连接上了", proxSocket.RemoteEndPoint.ToString()));
                ClientProxSocketList.Add(proxSocket);

                //不停地接收当前连接的客户端发送来的消息
                ThreadPool.QueueUserWorkItem(new WaitCallback(ReceiveData), proxSocket);
            }
        }
        //接收客户端的消息
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
                catch(Exception ex)
                {
                    //异常退出
                    AppendTextToTxtLog(string.Format("接收到客户端：{0}非正常退出", proxSocket.RemoteEndPoint.ToString()));
                    ClientProxSocketList.Remove(proxSocket);
                    StopConnect(proxSocket);
                    return;
                }
                if(len <= 0)
                {
                    //客户端正常退出
                    AppendTextToTxtLog(string.Format("客户端：{0}正常退出", proxSocket.RemoteEndPoint.ToString()));
                    ClientProxSocketList.Remove(proxSocket);
                    StopConnect(proxSocket);
                    return; //终结当前接收客户端数据的异步线程
                }
                //把接收到的数据放到文本框上去
                string str = Encoding.Default.GetString(data, 0, len);
                AppendTextToTxtLog(string.Format("接收到客户端：{0}的消息是：{1}",
                    proxSocket.RemoteEndPoint.ToString(), str));
            }
        }

        private void StopConnect(Socket proxSocket)
        {
            try
            {
                if (proxSocket.Connected)
                {
                    proxSocket.Shutdown(SocketShutdown.Both);
                    proxSocket.Close(100);
                }
            }
            catch (Exception ex)
            {

            }
        }

        //往日志的文本框上追加数据
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
        #region 发送字符串
        //发送消息
        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            foreach (Socket socket in ClientProxSocketList)
            {
                if (socket.Connected)
                {
                    //原始的字符串转成的字节数组
                    byte[] data = Encoding.Default.GetBytes(txtMsg.Text);
                    //对原始的数据数组加上协议的头部字节
                    byte[] result = new byte[data.Length + 1];
                    //设置当前的协议头部字节为1:1代表字符串
                    result[0] = 1;
                    //把原始的数据放到最终的字节数组里去
                    Buffer.BlockCopy(data, 0, result, 1, data.Length);
                    socket.Send(result, 0, result.Length, SocketFlags.None);
                }
            }
        }
        #endregion

        private void txtIP_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {

        }
        #region 发送闪屏
        private void btnSendShake_Click(object sender, EventArgs e)
        {
            foreach (Socket socket in ClientProxSocketList)
            {
                if (socket.Connected)
                {
                    socket.Send(new byte[] { 2 }, SocketFlags.None);
                }
            }
        }
        #endregion
        #region 发送文件
        private void btnSendFile_Click(object sender, EventArgs e)
        {
            using(OpenFileDialog ofd = new OpenFileDialog())
            {
                if(ofd.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                byte[] data = File.ReadAllBytes(ofd.FileName);
                byte[] result = new byte[data.Length + 1];
                result[0] = 3;
                Buffer.BlockCopy(data, 0, result, 1, data.Length);
                foreach (Socket socket in ClientProxSocketList)
                {
                    if (!socket.Connected)
                    {
                        continue;
                    }
                    //把要发送的文件读取出来
                    socket.Send(result, SocketFlags.None);
                }
            }
        }
        #endregion
    }
}
