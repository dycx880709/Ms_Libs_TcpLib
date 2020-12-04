using Ms.Libs.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ms.Libs.TcpLib
{
  
    public class TcpBase
    {
        static readonly Dictionary<string, string> errorCode = new Dictionary<string, string>();
        
        static TcpBase()
        {
            errorCode.Add("99997", "与服务器断开连接");
            errorCode.Add("99998", "超时");
            errorCode.Add("99999", "未知错误");
        }
       
        public static string GetErrorMessage(string code)
        {
            if (errorCode.ContainsKey(code))
                return errorCode[code];
            return "";
        }

        ConcurrentQueue<Packet> requestPackets = new ConcurrentQueue<Packet>();

        TcpClient client;
        //bool isStart = false;
        bool isStop = false;
        string host;
        int port;

        Task taskReceive = null;
        Task taskQueue = null;

        Dictionary<UInt32, List<Packet>> messages = new Dictionary<UInt32, List<Packet>>();
        object objMessage = new object();

        Timer timerKeepAlive;
        bool isSync;

        public bool IsConnected { get; set; }

        object lockConnectStatus = new object();

        Func<string, string> func;

        public TcpBase(string host, int port, bool isSync = false, Func<string, string> func = null)
        {
            this.isSync = isSync;
            this.host = host;
            this.port = port;
            this.func = func ?? GetErrorMessage;
        }

        public void SetServer(string host, int port)
        {
            this.host = host;
            this.port = port;
            if (client != null)
            {
                client.Close();
            }
        }

        #region KeepAlive

        public async void KeepAlive(object obj)
        {
            timerKeepAlive.Change(Timeout.Infinite, Timeout.Infinite);
            if (IsConnected)
            {
                await SendMessageAsync<bool>(0, 0, null, "", 10);
            }
            timerKeepAlive.Change(5000, 5000);
        }

        #endregion

        #region 连接

        public async Task<MsResult<bool>> Start()
        {
            MsResult<bool> msResult = new MsResult<bool>();
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(this.host, this.port);
                //isStart = true;
                isStop = false;

                taskQueue = RequestPacketQueue();
                taskReceive = ReceiveMessage();

                msResult.IsSuccess = true;
                IsConnected = true;

                if (timerKeepAlive == null)
                {
                    timerKeepAlive = new Timer(KeepAlive, null, 5000, 5000);
                }

                NotifyConnectStateChanged(new ConnectStateArgs { ConnectState = ConnectState.Success }, "");
            }
            catch (Exception ex)
            {
                msResult.IsSuccess = false;
                msResult.Error = ex.Message;
                NotifyConnectStateChanged(new ConnectStateArgs { ConnectState = ConnectState.Faild }, ex.ToString());
            }

            return msResult;
        }

        #endregion

        #region 断开

        public async Task Stop()
        {
            isStop = true;
            if (client != null)
            {
                client.Close();
            }
            if (taskReceive != null)
            {
                await taskReceive;
                taskReceive = null;
            }

            if (taskQueue != null)
            {
                await taskQueue;
                taskQueue = null;
            }
        }

        #endregion

        #region 事件

        /// <summary>
        /// 收到消息
        /// </summary>
        public event EventHandler<Packet> ReceiveMessaged;

        /// <summary>
        /// 连接状态改变
        /// </summary>
        public event EventHandler<ConnectStateArgs> ConnectStateChanged;

        private async void NotifyConnectStateChanged(ConnectStateArgs connectStateArgs, string error)
        {
            if (connectStateArgs.ConnectState == ConnectState.Faild)
            {
                IsConnected = false;
                lock (lockConnectStatus)
                {
                    //isStart = false;
                    if (client != null)
                    {
                        client.Close();
                    }
                }
                if (taskReceive != null) await taskReceive;
                //if (taskQueue != null) await taskQueue;
            }
            connectStateArgs.IsStop = isStop;
            connectStateArgs.Error = error;

            if (ConnectStateChanged != null)
            {
                var handlers = ConnectStateChanged.GetInvocationList();
                if (handlers != null)
                {
                    foreach (EventHandler<ConnectStateArgs> item in handlers)
                    {
                        item.BeginInvoke(this, connectStateArgs, null, null);
                    }
                }
            }

        }

        #endregion

        #region 发送消息

        object objIdentify = new object();
        UInt32 identify = 0;
        object objSend = new object();

        public Task<MsResult<T>> SendMessageAsyncMultiple<T>(byte module, byte method, object content, Action<MsResult<T>> action, string exchangeId = "", int timeout = 10)
        {
            return Task.Run(() =>
            {
                MsResult<T> msResult = new MsResult<T>();
                UInt32 identifyVar = 0;
                lock (objIdentify)
                {
                    identify++;
                    identifyVar = identify;
                }

                byte[] buffer = PacketHelper.Packet(module, method, RequestType.RequestReply, identifyVar, content, exchangeId);
                lock (objSend)
                {
                    try
                    {
                        if (client != null && client.Client != null && client.Client.Connected)
                        {
                            if (client != null && client.Client != null && client.Client.Connected)
                                client.GetStream().Write(buffer, 0, buffer.Length);
                            else
                            {
                                msResult.IsSuccess = false;
                                msResult.Error = func.Invoke("99997");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        msResult.IsSuccess = false;
                        msResult.Error = ex.Message;
                    }
                }
                var startTime = DateTime.Now;
                var loopTimes = 0;
                while (!msResult.IsFinished)
                {
                    loopTimes++;
                    //Console.WriteLine($"【{startTime.ToString("HH:mm:ss.ffff")}^^^{DateTime.Now.ToString("HH:mm:ss.ffff")}】:Not Finished");
                    lock (objMessage)
                    {
                        if (messages.ContainsKey(identifyVar))
                        {
                            var packets = messages[identifyVar];
                            foreach (var packet in packets)
                            {
                                if (packet.IsSuccess)
                                {
                                    try
                                    {
                                        msResult.Content = ConvertResult<T>(packet.Content);
                                        msResult.IsSuccess = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"SendMessageAsync exception: {ex.Message}");
                                        msResult.IsSuccess = false;
                                        msResult.Error = ex.Message;
                                    }
                                }
                                else
                                {
                                    msResult.IsSuccess = false;
                                    msResult.Error = func.Invoke(Encoding.UTF8.GetString(packet.Content));
                                }
                                action?.Invoke(msResult);
                            }
                            messages.Remove(identifyVar);
                        }
                        else if ((DateTime.Now - startTime).TotalSeconds > timeout)
                        {
                            msResult.Error = func.Invoke("99998");
                            msResult.IsSuccess = false;
                            break;
                        }
                    }
                    Thread.Sleep(10);
                }

                Console.WriteLine($"【{startTime.ToString("HH:mm:ss.ffff")}^^^{DateTime.Now.ToString("HH:mm:ss.ffff")}】:Finished");
                return msResult;
            });
        }

        public Task<MsResult<bool>> ReplyTerminal(byte module, byte method, object content, UInt32 identify, bool isSuccess, string exchangeId)
        {
            return Task.Run(() =>
            {
                MsResult<bool> msResult = new MsResult<bool>();

                byte[] buffer = PacketHelper.Packet(module, method, isSuccess ? RequestType.ResponseSuccess : RequestType.ResponseFaild, identify, content, exchangeId);
                lock (objSend)
                {
                    if (client != null && client.Client != null && client.Client.Connected)
                    {
                        try
                        {
                            client.GetStream().Write(buffer, 0, buffer.Length);
                        }
                        catch (Exception ex)
                        {
                            msResult.IsSuccess = false;
                            msResult.Error = ex.Message;
                        }
                    }
                    else
                    {
                        msResult.IsSuccess = false;
                        msResult.Error = func.Invoke("99997");
                    }
                }
                return msResult;
            });
        }

        public Task<MsResult<T>> SendMessageAsync<T>(byte module, byte method, object content, string exchangeId = "", int timeout = 10)
        {
            Console.WriteLine($"SendMessageAsync:{module}:{method}\t:{JsonConvert.SerializeObject(content)}");
            return Task.Run(() =>
            {
#if DEBUG
                //var stopwatch = new System.Diagnostics.Stopwatch();
                //stopwatch.Restart();
#endif
                MsResult<T> msResult = new MsResult<T>();
                UInt32 identifyVar = 0;
                lock (objIdentify)
                {
                    identify++;
                    identifyVar = identify;
                }

                byte[] buffer = PacketHelper.Packet(module, method, RequestType.RequestReply, identifyVar, content, exchangeId);
            
                lock (objSend)
                {
                    try
                    {
                        if (client != null && client.Client != null && client.Client.Connected)
                        {
                            if (client != null && client.Client != null && client.Client.Connected)
                            {
                                client.GetStream().Write(buffer, 0, buffer.Length);
                            }
                            else
                            {
                                msResult.IsSuccess = false;
                                msResult.Error = func.Invoke("99997");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        msResult.IsSuccess = false;
                        msResult.Error = ex.Message;
                    }
                }
                var startTime = DateTime.Now;

                while (true)
                {
                    if (module == 26 && method == 3)
                    {

                    }
                    lock (objMessage)
                    {
                        if (messages.ContainsKey(identifyVar))
                        {
                            var packets = messages[identifyVar];
                            foreach (var packet in packets)
                            {
                                if (module == 26 && method == 3)
                                {

                                }
                                
                                if (packet.IsSuccess)
                                {
                                    try
                                    {
                                        msResult.Content = ConvertResult<T>(packet.Content);
                                        msResult.IsSuccess = true;

                                        Console.WriteLine($"SendMessageAsync:{module}:{method}\t:Success【{(DateTime.Now - startTime).TotalMilliseconds}】");
                                    }
                                    catch (Exception ex)
                                    {
                                        //Console.WriteLine($"SendMessageAsync exception: {ex.Message}");
                                        msResult.IsSuccess = false;
                                        msResult.Error = ex.Message;
                                        Console.WriteLine($"SendMessageAsync:{module}:{method}\t:{msResult.Error}");
                                    }
                                }
                                else
                                {
                                    msResult.IsSuccess = false;
                                    msResult.Error = func.Invoke(Encoding.UTF8.GetString(packet.Content));
                                    Console.WriteLine($"SendMessageAsync:{module}:{method}\t:{msResult.Error}");
                                }
                            }
                            messages.Remove(identifyVar);
                            break;
                        }
                        else if ((DateTime.Now - startTime).TotalSeconds > timeout)
                        {
                            msResult.Error = func.Invoke("99998");
                            msResult.IsSuccess = false;

                            Console.WriteLine($"SendMessageAsync:{module}:{method}\t:{msResult.Error}");
                            break;
                        }
                    }
                    Thread.Sleep(10);
                }
#if DEBUG
                //Console.WriteLine($"SendMessageAsync module: {module} method: {method} expend:  {stopwatch.ElapsedMilliseconds}");
#endif
                return msResult;
            });
        }

        public Task<MsResult<bool>> SendMessageNoRelayAsync(byte module, byte method, object content, string exchangeId = "")
        {
            return Task.Run(() =>
            {
                MsResult<bool> msResult = new MsResult<bool>();
                UInt32 identifyVar = 0;
                lock (objIdentify)
                {
                    identify++;
                    identifyVar = identify;
                }

                byte[] buffer = PacketHelper.Packet(module, method, RequestType.RequestNoReply, identifyVar, content, exchangeId);
                lock (objSend)
                {
                    if (client != null && client.Client != null && client.Client.Connected)
                    {
                        try
                        {
                            client.GetStream().Write(buffer, 0, buffer.Length);
                        }
                        catch (Exception ex)
                        {
                            msResult.IsSuccess = false;
                            msResult.Error = ex.Message;
                        }
                    }
                    else
                    {
                        msResult.IsSuccess = false;
                        msResult.Error = func.Invoke("99997");
                    }
                }
                return msResult;
            });
        }

        public T ConvertResult<T>(byte[] buffer)
        {
            Type type = typeof(T);
            object result;
            if (type == typeof(string))
            {
                result = Convert.ChangeType(Encoding.UTF8.GetString(buffer), type);
            }
            else if (typeof(T) == typeof(byte[]))
            {
                result = Convert.ChangeType(buffer, type);
                if (result == null)
                {
                    result = new byte[0];
                }
            }
            else if (typeof(T) == typeof(bool))
            {
                if (buffer == null || buffer.Length == 0)
                {
                    result = Convert.ChangeType(true, type);
                }
                else
                {
                    result = Convert.ChangeType(buffer[0], type);
                }
            }
            else if (type == typeof(int))
            {
                result = Convert.ToInt32(Encoding.UTF8.GetString(buffer));
            }
            else if (type == typeof(long) || type == typeof(Int64))
            {
                result = Convert.ChangeType(IPAddress.NetworkToHostOrder(BitConverter.ToInt64(buffer, 0)), type);
            }
            else
            {
                var p = Encoding.UTF8.GetString(buffer);
                try
                {
                    //Console.WriteLine(p); 
                    result = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(buffer), type);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ConvertResult Exception:{ex.Message}");
                    //LogHelper.Instance.Error(ex);
                    result = null;
                    throw ex;
                }
            }
            return (T)result;
        }

        #endregion

        #region 接收消息

        private Task RequestPacketQueue()
        {
            return Task.Run(() =>
            {
                while (!isStop)
                {
                    Packet packet;
                    if (requestPackets.TryDequeue(out packet))
                    {
                        if (ReceiveMessaged != null)
                        {
                            if (isSync)
                            {
                                ReceiveMessaged(this, packet);
                            }
                            else
                            {
                                var handlers = ReceiveMessaged.GetInvocationList();
                                if (handlers != null)
                                {
                                    foreach (EventHandler<Packet> item in handlers)
                                    {
                                        item.BeginInvoke(this, packet, null, null);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
            });
        }

        private Task ReceiveMessage()
        {
            return Task.Run(() =>
            {
                try
                {
                    byte[] buffer = new byte[1024 * 1024 * 10];
                    List<byte> tmpBuffer = new List<byte>();
                    while (true)
                    {
                        if(client == null || client.Client == null)
                            throw new Exception("client is null");

                        int count = client.Client.Receive(buffer);
                        if (count == 0)
                            throw new Exception("time out");

                        tmpBuffer.AddRange(buffer.Take(count));
                        tmpBuffer = PacketHelper.Divide(tmpBuffer, new Action<Packet>(packet =>
                        {
                            if (packet.IsReply)
                            {
                                lock (objMessage)
                                {
                                    if (messages.ContainsKey(packet.Identify))
                                        messages[packet.Identify].Add(packet);
                                    else
                                        messages[packet.Identify] = new List<Packet>() { packet };
                                }
                            }
                            else
                            {
                                requestPackets.Enqueue(packet);
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ReceiveMessage Exception{ex.Message}");
                    NotifyConnectStateChanged(new ConnectStateArgs { ConnectState = ConnectState.Faild }, ex.Message);
                }
            });
        }

        #endregion
    }
}
