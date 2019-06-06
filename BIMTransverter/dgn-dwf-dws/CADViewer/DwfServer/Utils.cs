using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DwfServer
{
    class Utils
    {
        /// <summary>
        /// 封包
        /// </summary>
        /// <param name="headerBytes"></param>
        /// <param name="bodyBytes"></param>
        /// <returns></returns>
        public static byte[] GetSendBuffer(byte[] headerBytes, byte[] bodyBytes)
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
        /// <summary>
        /// 删除文件夹及其子文件
        /// </summary>
        /// <param name="path"></param>
        public static void delDir(string path)
        {
            foreach (string d in Directory.GetFileSystemEntries(path))
            {
                File.Delete(d);
            }
            Directory.Delete(path);
        }
    }
}
