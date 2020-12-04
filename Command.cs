namespace Ms.Libs.TcpLib
{
    public class BusinessCommand
    {

        public const byte ModuleSystem = 0;
        public const byte SystemKeepalive = 0;
        public const byte Login = 1;
        public const byte SystemUpdate = 2;
        public const byte SystemReply = 3;
        public const byte OnlineChanged = 4;
        public const byte CheckUpdate = 5;
        public const byte PublishUpdate = 6;

        /// <summary>
        /// 系统类型
        /// </summary>
        public const byte TypeFile = 1;
        /// <summary>
        /// 上传文件
        /// </summary>
        public const byte FileUpload = 0;
        /// <summary>
        /// 下载文件
        /// </summary>
        public const byte FileDownload = 1;
        /// <summary>
        /// 删除文件
        /// </summary>
        public const byte FileDelete = 2;
        /// <summary>
        /// 文件信息
        /// </summary>
        public const byte FileInfo = 3;

        public const byte UploadForwardFile = 4;
        public const byte UploadForwardFileResult = 5;
    }
}
