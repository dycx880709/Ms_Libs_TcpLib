using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ms.Libs.TcpLib
{
    public class TcpProxyPool
    {
        List<TcpProxyThread> tcpProxyThreads = new List<TcpProxyThread>();
        string host;
        int port;
        string token;
        int maxTcpProxy;

        AutoResetEvent are = new AutoResetEvent(true);

        public TcpProxyPool(string host, int port, int maxTcpProxy)
        {
            this.host = host;
            this.port = port;
            this.maxTcpProxy = maxTcpProxy;
            //this.token = token;
        }

        public void SetServer(string host, int port)
        {
            this.host = host;
            this.port = port;
        }

        public void SetToken(string token)
        {
            this.token = token;
        }

        public async Task<TcpProxyThread> GetTcpProxy()
        {
            try
            {
                are.WaitOne();
                foreach (var item in tcpProxyThreads)
                {
                    if (!item.IsUsed)
                    {
                        item.IsUsed = true;
                        return item;
                    }
                }
                if (tcpProxyThreads.Count >= maxTcpProxy)
                {
                    return null;
                }
                TcpBase tcpProxy = new TcpBase(host, port);

                var result = await tcpProxy.Start();
                if (result.IsSuccess)
                {
                    var tcpProxyThread = new TcpProxyThread
                    {
                        IsUsed = true,
                        TcpProxy = tcpProxy
                    };
                    tcpProxy.ConnectStateChanged += TcpProxy_ConnectStateChanged;
                    tcpProxyThreads.Add(tcpProxyThread);
                    return tcpProxyThread;

                    //if (await Login(tcpProxy))
                    //{
                    //    var tcpProxyThread = new TcpProxyThread
                    //    {
                    //        IsUsed = true,
                    //        TcpBase = tcpProxy
                    //    };
                    //    tcpProxy.ConnectStateChanged += TcpProxy_ConnectStateChanged;
                    //    tcpProxyThreads.Add(tcpProxyThread);
                    //    return tcpProxyThread;
                    //}
                    //return tcpProxyThreads.Count > 0 ? tcpProxyThreads[0] : null;
                }
            }
            catch
            {

            }
            finally
            {
                are.Set();
            }
            return null;
        }

        private void TcpProxy_ConnectStateChanged(object sender, ConnectStateArgs e)
        {
            switch (e.ConnectState)
            {
                case ConnectState.Success:
                    //await Login(sender as TcpBase);
                    break;
                case ConnectState.Faild:
                    are.WaitOne();
                    var t = tcpProxyThreads.FirstOrDefault(f => f.TcpProxy == sender);
                    if (t != null)
                    {
                        tcpProxyThreads.Remove(t);
                    }
                    are.Set();
                    break;
            }
        }

        private async Task<bool> Login(TcpBase tcpProxy)
        {
            var loginResult = await tcpProxy.SendMessageAsync<bool>(BusinessCommand.ModuleSystem, BusinessCommand.Login, token);
            return loginResult.IsSuccess;
        }

        public void BackThread(TcpProxyThread tcpProxyThread)
        {
            are.WaitOne();
            tcpProxyThread.IsUsed = false;
            are.Set();
        }

        public async void DisposeThreads()
        {
            are.WaitOne();
            foreach (var item in tcpProxyThreads)
            {
                await item.TcpProxy.Stop();
            }
            tcpProxyThreads.Clear();
            are.Set();
        }
    }

    public class TcpProxyThread
    {
        public TcpBase TcpProxy { get; set; }

        public bool IsUsed { get; set; }
    }
}
