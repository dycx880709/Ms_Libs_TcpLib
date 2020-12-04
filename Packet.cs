using System;

namespace Ms.Libs.TcpLib
{
    public class Packet : EventArgs
    {
        /// <summary>
        /// 模块
        /// </summary>
        public byte Module { get; set; }

        /// <summary>
        /// 方法
        /// </summary>
        public byte Method { get; set; }

        /// <summary>
        /// 是否回复
        /// </summary>
        public bool IsReply { get; set; }

        /// <summary>
        /// 标识
        /// </summary>
        public UInt32 Identify { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// 交换ID
        /// </summary>
        public string ExchangeID { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

    }
}
