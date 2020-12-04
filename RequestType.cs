namespace Ms.Libs.TcpLib
{
    public enum RequestType
    {

        /// <summary>
        /// 请求服务器
        /// </summary>
        RequestReply = 0,

        RequestNoReply = 1,

        /// <summary>
        /// 请求反馈成功
        /// </summary>
        ResponseSuccess = 2,
        /// <summary>
        /// 请求反馈失败
        /// </summary>
        ResponseFaild = 3,
        /// <summary>
        /// 服务请求
        /// </summary>
        ServerRequest = 4
    }
}
