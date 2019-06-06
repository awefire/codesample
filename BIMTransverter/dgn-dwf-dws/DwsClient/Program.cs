using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;

namespace DwsClient
{
    class Program
    {
        static TcpPullClient client = new TcpPullClient();
        // 包头大小
        static int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        static PkgInfo pkgInfo = new PkgInfo();
        static int id = 0;

        static string dwsFile { get; set; }
        static string jpgFile { get; set; }
        static void Main(string[] args)
        {
            dwsFile = args[0];jpgFile = dwsFile.Replace(".dws",".jpg");
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;
            if (client.Connect("192.168.2.18", 1140) == true)
            {
                using (var fs = new FileStream(dwsFile, FileMode.Open))
                {
                    // 封包体
                    byte[] bodyBytes = new byte[fs.Length];
                    fs.Read(bodyBytes, 0, bodyBytes.Length);
                    fs.Close();
                    // 封包头
                    PkgHeader header = new PkgHeader();
                    header.Id = ++id;
                    header.BodySize = bodyBytes.Length;
                    byte[] headerBytes = client.StructureToByte<PkgHeader>(header);


                    // 组合最终发送的封包 (封包头+封包体)
                    byte[] sendBytes = GetSendBuffer(headerBytes, bodyBytes);
                    client.Send(sendBytes, sendBytes.Length);
                }
            }
            client.OnReceive += Client_OnReceive;
            System.Diagnostics.Process.GetCurrentProcess().WaitForExit();
        }

        private static HandleResult Client_OnReceive(TcpPullClient sender, int length)
        {
            #region 收数据
            // 需要长度
            int required = pkgInfo.Length;

            // 剩余大小
            int remain = length;

            while (remain >= required)
            {
                IntPtr bufferPtr = IntPtr.Zero;
                try
                {
                    remain -= required;
                    bufferPtr = Marshal.AllocHGlobal(required);
                    if (client.Fetch(bufferPtr, required) == FetchResult.Ok)
                    {
                        if (pkgInfo.IsHeader == true)
                        {
                            PkgHeader header = (PkgHeader)Marshal.PtrToStructure(bufferPtr, typeof(PkgHeader));

                            required = header.BodySize;
                        }
                        else
                        {
                            //intptr转byte[]
                            byte[] bytes = new byte[required];
                            Marshal.Copy(bufferPtr, bytes, 0, required);
                            using (var fs = new FileStream(jpgFile, FileMode.Create))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                                fs.Close();
                            }

                            required = pkgHeaderSize;
                        }

                        // 在后面赋值,因为前面需要用到pkgInfo.Length
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (client.SetExtra(pkgInfo) == false)
                        {
                            return HandleResult.Error;
                        }
                    }

                }
                catch
                {
                    return HandleResult.Error;
                }
                finally
                {
                    if (bufferPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(bufferPtr);
                        bufferPtr = IntPtr.Zero;
                    }
                }
            }
            #endregion
            if (File.Exists(jpgFile))
            {
                Console.WriteLine("succeed");
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            return HandleResult.Ok;
        }

        static byte[] GetSendBuffer(byte[] headerBytes, byte[] bodyBytes)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                int bufferSize = headerBytes.Length + bodyBytes.Length;
                ptr = Marshal.AllocHGlobal(bufferSize);

                // 拷贝包头到缓冲区首部
                Marshal.Copy(headerBytes, 0, ptr, headerBytes.Length);

                // 拷贝包体到缓冲区剩余部分
                Marshal.Copy(bodyBytes, 0, ptr + headerBytes.Length, bodyBytes.Length);

                byte[] bytes = new byte[bufferSize];
                Marshal.Copy(ptr, bytes, 0, bufferSize);

                return bytes;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public class PkgHeader
    {
        public int Id;
        public int BodySize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class PkgInfo
    {
        public bool IsHeader;
        public int Length;
    }
}
