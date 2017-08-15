using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ZhongcaiSocket
{
    /// <summary>
    /// ClassName:SocketHelper
    /// Auther：Yinzhongcai
    /// </summary>
    public class SocketHelper
    {
        public delegate void PushSockets(Sockets sockets);
        public static PushSockets pushSockets;
        
        
        /// <summary>
        /// 转2进制
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private byte[] String2Byte(string str)
        {
            byte[] b_buf = new byte[Convert.ToInt32((int)(str.Length / 2))];
            for (int i = 0; i < b_buf.Length; ++i)
            {
                b_buf[i] = Convert.ToByte(str.Substring(i * 2, 2), 0x10);

            }
            return b_buf;
        }
        /// <summary>
        /// 字节转字符串
        /// </summary>
        /// <param name="b_buf"></param>
        /// <returns></returns>
        private string Hex2String(byte[] b_buf)
        {
            StringBuilder sb_value = new StringBuilder();
            foreach(byte l_byte in b_buf)
            {
                sb_value.Append(Convert.ToString(l_byte, 0x10).PadLeft(2, '0'));
            }
            return sb_value.ToString();
        }
        /// <summary>
        /// Socket抽象类
        /// </summary>
        public abstract class SocketObject
        {
            public abstract void InitSocket(IPAddress ipaddress, int port);
            public abstract void InitSocket(string ipaddress,int port);
            public abstract void Start();
            public abstract void Stop();
        }

        /// <summary>
        /// 以下是Socket对象
        /// </summary>
        public class Sockets
        {
            /// <summary>
            /// 接收缓存区
            /// </summary>
            public byte[] RecBuffer = new byte[8 * 1024];
            /// <summary>
            /// 发送缓存区
            /// </summary>
            public byte[] SendBuffer = new byte[8 * 1024];
            /// <summary>
            /// 异步接收后包大小
            /// </summary>
            public int Offset { get; set; }
            
            /// <summary>
            ///IP地址啊
            /// </summary>
            public IPEndPoint Ip { get; set; }
            /// <summary>
            /// Client啊
            /// </summary>
            public TcpClient Client { get; set; }
            /// <summary>
            /// 网络流啊
            /// </summary>
            public NetworkStream nStream { get; set; }
            /// <summary>
            /// 异常
            /// </summary>
            public Exception ex { get; set; }
            public bool NewClientFlag { get; set; }
            public bool ClientDispose { get; set; }
            public Sockets() { }

            public Sockets(IPEndPoint ip, TcpClient client, NetworkStream ns)
            {
                Ip = ip;
                Client = client;
                nStream = ns;
            }
        }

        #region Tcp服务端
        public class TcpServer : SocketObject
        {
            bool IsStop = false;
            object obj = new object();
            /// <summary>
            /// 信号量啊
            /// </summary>
            private Semaphore semap = new Semaphore(5, 5000);
            /// <summary>
            /// 客户端队列
            /// </summary>
            public List<Sockets> ClientList = new List<Sockets>();
            /// <summary>
            /// 服务端
            /// </summary>
            public TcpListener listener;

            public IPAddress Ipaddress;

            private string boundary = "welcome to wanji";
            /// <summary>
            /// 端口啊
            /// </summary>
            private int Port;
            /// <summary>
            /// IP啊
            /// </summary>
            private IPEndPoint ip;
            /// <summary>
            /// 初始化服务端对象
            /// </summary>
            /// <param name="ipaddress"></param>
            /// <param name="port"></param>
            public override void InitSocket(IPAddress ipaddress, int port)
            {
                Ipaddress = ipaddress;
                Port = port;
                listener = new TcpListener(Ipaddress, Port);
            }
            /// <summary>
            /// +1重载 初始化服务端对象
            /// </summary>
            /// <param name="ipaddress"></param>
            /// <param name="port"></param>
            public override void InitSocket(string ipaddress,int port)
            {
                Ipaddress = IPAddress.Parse(ipaddress);
                Port = port;
                ip = new IPEndPoint(Ipaddress, Port);
                listener = new TcpListener(Ipaddress, Port);
            }
            /// <summary>
            /// 启动监听
            /// </summary>
            public override void Start()
            {
                try
                {
                    listener.Start();
                    Thread Accth = new Thread(new ThreadStart(delegate
                        {
                            while (true)
                            {
                                if (IsStop != false)
                                {
                                    break;
                                }
                                GetAcceptTcpClinet();
                                Thread.Sleep(1);
                            }
                        }));
                    Accth.Start();
                }
                catch (SocketException skex)
                {
                    Sockets sk = new Sockets();
                    sk.ex = skex;
                    pushSockets.Invoke(sk);
                }
                //throw new NotImplementedException();
            }

            /// <summary>
            /// 处理新的链接
            /// </summary>
            private void GetAcceptTcpClinet()
            {
                try
                {
                    if (listener.Pending())
                    {
                        semap.WaitOne();
                        TcpClient tclient = listener.AcceptTcpClient();

                        Socket socket = tclient.Client;
                        NetworkStream stream = new NetworkStream(socket, true);
                        Sockets sks = new Sockets(tclient.Client.RemoteEndPoint as IPEndPoint, tclient, stream);
                        //加入客户端集合
                        AddClientList(sks);
                        sks.NewClientFlag = true;
                        pushSockets.Invoke(sks);
                        //客户端异步接收
                        sks.nStream.BeginRead(sks.RecBuffer, 0, sks.RecBuffer.Length, new AsyncCallback(EndReader), sks);
                        
                        
                        //if (stream.CanWrite)
                        //{
                        //    byte[] buffer = Encoding.Default.GetBytes(boundary);
                        //    stream.Write(buffer, 0, buffer.Length);
                        //}
                        semap.Release();
                    }
                }
                catch (Exception ex)
                {
                    return;
                }
            }
            /// <summary>
            /// 异步接收发送的数据
            /// </summary>
            /// <param name="ir"></param>
            private void EndReader(IAsyncResult ir)
            {
                Sockets sks = ir.AsyncState as Sockets;
                if (sks != null && listener != null)
                {
                    try
                    {
                        if (sks.NewClientFlag || sks.Offset != 0)
                        {
                            sks.NewClientFlag = false;
                            
                            pushSockets.Invoke(sks);
                            sks.nStream.BeginRead(sks.RecBuffer, 0, sks.RecBuffer.Length, new AsyncCallback(EndReader), sks);
                            sks.Offset = sks.nStream.EndRead(ir);
                            

                        }
                    }
                    catch (Exception skex)
                    {
                        lock (obj)
                        {
                            //移除异常
                            ClientList.Remove(sks);
                            Sockets sk = sks;
                            sk.ClientDispose = true;//让客户端滚粗(感觉这里不太好啊)
                            sk.ex = skex;
                            pushSockets.Invoke(sks);
                        }
                    }
                }
            }
            /// <summary>
            /// 加入队列
            /// </summary>
            /// <param name="sk"></param>
            private void AddClientList(Sockets sk)
            {
                lock (obj)
                {
                    Sockets sockets = ClientList.Find(o => { return o.Ip == sk.Ip; });
                    if (sockets==null)
                    {
                        ClientList.Add(sk);
                    }
                    else
                    {
                        ClientList.Remove(sockets);
                        ClientList.Add(sk);

                    }
                }
            }
            /// <summary>
            /// Stop
            /// </summary>
            public override void Stop()
            {
                if (listener != null)
                {
                    listener.Stop();
                    listener = null;
                    IsStop = true;
                    SocketHelper.pushSockets = null;
                }
            }

            /// <summary>
            /// 向客户端发送数据
            /// </summary>
            /// <param name="ip">客户端IP+端口</param>
            /// <param name="SendData">发送的数据包</param>
            public void SendToClient(IPEndPoint ip, string SendData)
            {
                try
                {
                    Sockets sks = ClientList.Find(o => { return o.Ip == ip; });
                    if (sks==null||!sks.Client.Connected)
                    {
                        //无连接
                        Sockets ks = new Sockets();
                        sks.ClientDispose = true;
                        sks.ex = new Exception("客户端无连接");
                        pushSockets.Invoke(sks);

                    }
                    if (sks.Client.Connected)
                    {
                        NetworkStream nStream = sks.nStream;
                        if (nStream.CanWrite)
                        {
                            byte[] buffer = Encoding.Default.GetBytes(SendData);
                            nStream.Write(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            nStream = sks.Client.GetStream();
                            if (nStream.CanWrite)
                            {
                                byte[] buffer = Encoding.Default.GetBytes(SendData);
                                nStream.Write(buffer, 0, buffer.Length);

                            }
                            else
                            {
                                ClientList.Remove(sks);
                                Sockets ks = new Sockets();
                                sks.ClientDispose = true;
                                sks.ex = new Exception("客户端无连接");
                                pushSockets.Invoke(sks);
                            }
                        }
                    }
                }
                catch (Exception skex)
                {
                    Sockets sks = new Sockets();
                    sks.ClientDispose = true;
                    sks.ex = skex;
                    pushSockets.Invoke(sks);
                }
            }

            public void SendHexToClient(IPEndPoint ip, byte[] buf)
            {
                try
                {
                    Sockets sks = ClientList.Find(o => { return o.Ip == ip; });
                    if (sks == null || !sks.Client.Connected)
                    {
                        //无连接
                        Sockets ks = new Sockets();
                        sks.ClientDispose = true;
                        sks.ex = new Exception("客户端无连接");
                        pushSockets.Invoke(sks);

                    }
                    if (sks.Client.Connected)
                    {
                        NetworkStream nStream = sks.nStream;
                        if (nStream.CanWrite)
                        {

                            nStream.Write(buf, 0, buf.Length);
                        }
                        else
                        {
                            nStream = sks.Client.GetStream();
                            if (nStream.CanWrite)
                            {

                                nStream.Write(buf, 0, buf.Length);

                            }
                            else
                            {
                                ClientList.Remove(sks);
                                Sockets ks = new Sockets();
                                sks.ClientDispose = true;
                                sks.ex = new Exception("客户端无连接");
                                pushSockets.Invoke(sks);
                            }
                        }
                    }
                }
                catch (Exception skex)
                {
                    Sockets sks = new Sockets();
                    sks.ClientDispose = true;
                    sks.ex = skex;
                    pushSockets.Invoke(sks);
                }
            }
        }

        #endregion

        #region Tcp客户端

        public class TcpClients : SocketObject
        {
            bool IsClose = false;
            /// <summary>
            /// 当前管理对象
            /// </summary>
            Sockets sk;
            /// <summary>
            /// 客户端
            /// </summary>
            TcpClient client;
            /// <summary>
            /// 当前连接服务端地址
            /// </summary>
            IPAddress Ipaddress;
            /// <summary>
            /// 当前连接服务端端口号
            /// </summary>
            int Port;
            /// <summary>
            /// 服务端IP+端口
            /// </summary>
            IPEndPoint ip;
            /// <summary>
            /// 发送与接收使用的流
            /// </summary>
            NetworkStream nStream;
            /// <summary>
            /// 初始化Socket
            /// </summary>
            /// <param name="ipaddress"></param>
            /// <param name="port"></param>
            public override void InitSocket(string ipaddress, int port)
            {
                Ipaddress = IPAddress.Parse(ipaddress);
                Port = port;
                ip = new IPEndPoint(Ipaddress, Port);
                client = new TcpClient();
            }
            public void SendData(string SendData)
            {
                try
                {

                    if (client == null || !client.Connected)
                    {
                        Sockets sks = new Sockets();
                        sks.ex = new Exception("客户端无连接..");
                        sks.ClientDispose = true;
                        pushSockets.Invoke(sks);//推送至UI 
                    }
                    if (client.Connected) //如果连接则发送
                    {
                        if (nStream == null)
                        {
                            nStream = client.GetStream();
                        }
                        byte[] buffer = Encoding.Default.GetBytes(SendData);
                        nStream.Write(buffer, 0, buffer.Length);
                    }
                }
                catch (Exception skex)
                {
                    Sockets sks = new Sockets();
                    sks.ex = skex;
                    sks.ClientDispose = true;
                    pushSockets.Invoke(sks);//推送至UI
                }
            }
            /// <summary>
            /// 初始化Socket
            /// </summary>
            /// <param name="ipaddress"></param>
            /// <param name="port"></param>
            public override void InitSocket(IPAddress ipaddress, int port)
            {
                Ipaddress = ipaddress;
                Port = port;
                ip = new IPEndPoint(Ipaddress, Port);
                client = new TcpClient();
            }
            private void Connect()
            {
                client.Connect(ip);
                nStream = new NetworkStream(client.Client, true);
                sk = new Sockets(ip, client, nStream);
                sk.nStream.BeginRead(sk.RecBuffer, 0, sk.RecBuffer.Length, new AsyncCallback(EndReader), sk);
            }
            private void EndReader(IAsyncResult ir)
            {

                Sockets s = ir.AsyncState as Sockets;
                try
                {
                    if (s != null)
                    {

                        if (IsClose && client == null)
                        {
                            sk.nStream.Close();
                            sk.nStream.Dispose();
                            return;
                        }
                        s.Offset = s.nStream.EndRead(ir);
                        pushSockets.Invoke(s);//推送至UI
                        sk.nStream.BeginRead(sk.RecBuffer, 0, sk.RecBuffer.Length, new AsyncCallback(EndReader), sk);
                    }
                }
                catch (Exception skex)
                {
                    Sockets sks = s;
                    sks.ex = skex;
                    sks.ClientDispose = true;
                    pushSockets.Invoke(sks);//推送至UI

                }

            }
            /// <summary>
            /// 重写Start方法,其实就是连接服务端
            /// </summary>
            public override void Start()
            {
                Connect();
            }
            public override void Stop()
            {
                Sockets sks = new Sockets();
                if (client != null)
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                    Thread.Sleep(10);
                    client.Close();
                    IsClose = true;
                    client = null;
                }
                else
                {
                    sks.ex = new Exception("客户端没有初始化.!");
                }
                pushSockets.Invoke(sks);//推送至UI
            }

        }
        #endregion
    }
}
