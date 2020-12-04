using System;

namespace Ms.Libs.TcpLib
{
    public class ConnectStateArgs : EventArgs
    {
        public ConnectState ConnectState { get; set; }
        public bool IsStop { get; set; }
        public string Error { get; set; }
    }

    public enum ConnectState
    {
        Success,
        Faild
    }
}
