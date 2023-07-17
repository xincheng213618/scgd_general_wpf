using OpenH264Lib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorVision.Util
{
    public class H264Reader
    {
        private Dictionary<int, List<byte[]>> packets;
        private int headLen;

        public H264Reader()
        {
            packets = new Dictionary<int, List<byte[]>>();
            headLen = 4;
        }
        public byte[]? AddPacket(byte[] buffer)
        {
            byte[] bytes;
            Console.WriteLine("{0} => UDPMessageReceived={1}", DateTime.Now.ToString("mm:ss.ffff"), buffer[3]);
            if (buffer[0] == 0)
            {
                int len = buffer.Length - headLen;
                bytes = new byte[len];
                Buffer.BlockCopy(buffer, headLen, bytes, 0, len);
                return bytes;
            }
            else
            {
                int len = buffer.Length - headLen;
                bytes = new byte[len];
                Buffer.BlockCopy(buffer, headLen, bytes, 0, len);
                if (!packets.ContainsKey(buffer[0]))
                {
                    packets.Add(buffer[0], new List<byte[]>());
                }
                packets[buffer[0]].Add(bytes);
                //Console.WriteLine("key={0},list={1},head={2}/{3}", args.Buffer[0], data[args.Buffer[0]].Count, args.Buffer[1], args.Buffer[2]);
                if (packets[buffer[0]].Count == buffer[1])
                {
                    int totalLen = 0;
                    foreach (var item in packets[buffer[0]])
                    {
                        totalLen += item.Length;
                    }

                    bytes = new byte[totalLen];
                    int pos = 0;
                    for (int i = 0; i < buffer[1]; i++)
                    {
                        Buffer.BlockCopy(packets[buffer[0]][i], 0, bytes, pos, packets[buffer[0]][i].Length);
                        pos += packets[buffer[0]][i].Length;
                    }
                    //////////////////////////////////////
                    packets.Remove(buffer[0]);
                    Console.WriteLine("Remove key={0}", buffer[0]);
                    return bytes;
                }
            }

            return null;
        }
    }
}
