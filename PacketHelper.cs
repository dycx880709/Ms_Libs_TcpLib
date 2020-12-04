using Google.Protobuf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Ms.Libs.TcpLib
{
    public static class PacketHelper
    {
        //包头
        private static byte[] HEADER = Encoding.UTF8.GetBytes("MS\n");

        //基础包长度 包头3  长度字节4 命令类型1 命令方法1 请求类型1 标识1 交换ID长度1
        private const int BASEPACKETLENGTH = 15;

        public static byte[] Packet(byte module, byte method, RequestType requestType, UInt32 identify, object content, string exchangeId)
        {
            List<byte> data = new List<byte>();
            data.AddRange(HEADER);

            byte[] contentArray = null;

            if (content == null)
            {
                contentArray = new byte[0];
            }
            else if (content.GetType() == typeof(string))
            {
                contentArray = Encoding.UTF8.GetBytes(content.ToString());
            }
            else if (content.GetType() == typeof(int) || content.GetType() == typeof(long) || content.GetType() == typeof(float))
            {
                contentArray = Encoding.UTF8.GetBytes(content.ToString());
            }
            else if (content.GetType() == typeof(bool))
            {
                contentArray = new byte[] { bool.Parse(content.ToString()) ? (byte)1 : (byte)0 };
            }
            else if (content.GetType() == typeof(byte[]))
            {
                contentArray = content as byte[];
            }
            else
            {
                if (content is Google.Protobuf.IMessage)
                {
                    IMessage iMessage = content as IMessage;
                    contentArray = iMessage.ToByteArray();
                }
                else
                {
                    contentArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(content));
                }

            }

            byte[] exchangeIdArray = exchangeId == null ? new byte[0] : Encoding.UTF8.GetBytes(exchangeId.ToString());
            data.AddRange(BitConverter.GetBytes((Int32)IPAddress.HostToNetworkOrder(contentArray.Length + BASEPACKETLENGTH + exchangeIdArray.Length)));
            data.Add(module);
            data.Add(method);
            data.Add((byte)requestType.GetHashCode());
            data.AddRange(BitConverter.GetBytes((Int32)IPAddress.HostToNetworkOrder((Int32)identify)));
            data.Add((byte)exchangeIdArray.Length);
            data.AddRange(exchangeIdArray);
            data.AddRange(contentArray);
            return data.ToArray();
        }

        public static Packet UnPack(byte[] datas)
        {

            Packet packet = new Packet();
            packet.Module = datas[0];
            packet.Method = datas[1];

            switch ((RequestType)datas[2])
            {
                case RequestType.ResponseFaild:
                    packet.IsSuccess = false;
                    packet.IsReply = true;
                    break;
                case RequestType.ResponseSuccess:
                    packet.IsSuccess = true;
                    packet.IsReply = true;
                    break;
                case RequestType.ServerRequest:
                    packet.IsSuccess = true;
                    packet.IsReply = false;
                    break;
            }

            packet.Identify = (UInt32)IPAddress.HostToNetworkOrder((BitConverter.ToInt32(datas.ToArray(), 3)));
            packet.ExchangeID = Encoding.UTF8.GetString(datas, 8, datas[7]);

            packet.Content = datas.Skip(8 + datas[7]).ToArray();

            return packet;
        }

        public static List<byte> Divide(List<byte> datas, Action<Packet> action)
        {
            int length = 0;
            int i = 0;

            while (datas.Count >= i + BASEPACKETLENGTH)
            {

                if (datas[i] == HEADER[0] && datas[i + 1] == HEADER[1] && datas[i + 2] == HEADER[2])
                {
                    length = IPAddress.HostToNetworkOrder((BitConverter.ToInt32(datas.ToArray(), i + 3)));
                    if (datas.Count >= i + length)
                    {

                        var data = datas.Skip(i + 7).Take(length - 7).ToList();
                        action(UnPack(data.ToArray()));
                        i = i + length;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    throw new Exception("divide error");
                }
            }
            return datas.Skip(i).ToList();
        }
    }
}
