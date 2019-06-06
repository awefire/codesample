using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;

namespace RvtClient
{
    class Program
    {
        static TcpPullClient client = new TcpPullClient();
        // 包头大小
        private static int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static PkgInfo pkgInfo = new PkgInfo();
        private static int id = 0;
        static string rvtName { get; set; }
        static string ifcName { get; set; }

        static void Main(string[] args)
        {
            rvtName = args[0];
            ifcName = rvtName.Replace(".rvt",".ifc");
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;

            if(client.Connect("192.168.2.18",1136) == true)
            {
                using (var fs = new FileStream(rvtName, FileMode.Open))
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
                    byte[] sendBytes = Utils.GetSendBuffer(headerBytes, bodyBytes);
                    client.Send(sendBytes, sendBytes.Length);
                }
            }
            client.OnReceive += Client_OnReceive;
            client.OnSend += Client_OnSend;

            System.Diagnostics.Process.GetCurrentProcess().WaitForExit();
        }

        private static HandleResult Client_OnSend(TcpClient sender, byte[] bytes)
        {
            return HandleResult.Ok;
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
                            using (var fs = new FileStream(ifcName, FileMode.Create))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                                fs.Close();
                                FileInfo info = new FileInfo(ifcName);
                                info.Attributes = FileAttributes.Hidden;
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
            if (File.Exists(ifcName))
            {
                CmdCommand(rvtName);//此处传入rvt文件为BimJson重定向
                if(File.Exists(ifcName.Replace(".ifc","_M.json")))
                {
                    File.Delete(ifcName);
                    Console.WriteLine("succeed");
                    System.Diagnostics.Process.GetCurrentProcess().Kill();//kill客户端
                }
            }
           
            return HandleResult.Ok;
        }

        /// <summary>
        /// 在程序窗口中调用cmd命令行
        /// </summary>
        public static void CmdCommand(string fileName)
        {
            string str1 =  Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\BimJson.exe "+fileName;
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.UseShellExecute = false;    //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;//不显示程序窗口
            p.Start();//启动程序

            //向cmd窗口发送输入信息
            p.StandardInput.WriteLine(str1 + "&exit");
            p.StandardInput.AutoFlush = true;

            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
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
