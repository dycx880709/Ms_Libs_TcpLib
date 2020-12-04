using System;
using System.Net;

namespace Ms.Libs.Models
{
    public class MsResult<T>
    {
        /// <summary>
        /// 返回内容
        /// </summary>
        public T Content { get; set; }

        /// <summary>
        /// 标识
        /// </summary>
        public UInt32 Identify { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// HTTP状态代码
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; set; }


        public bool IsFinished { get; set; }

        public bool IsCanncel { get; set; }
    }
}
