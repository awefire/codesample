using HPSocketCS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dwg2Json
{
    class Program
    {
        public static TcpPullClient client = new TcpPullClient();

        // 包头大小
        public static int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        public static PkgInfo pkgInfo = new PkgInfo();
        public static int id = 0;
        public static string dwgFile { get; set; }
        public static string dwgPath { get; set; }
        public static string zipFile { get; set; }
        static void Main(string[] args)
        {
            dwgFile = args[0];
            dwgPath = dwgFile.Replace(".dwg","")+"\\";
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;
            if (client.Connect("192.168.2.18", 1137) == true)
            {
                using (var fs = new FileStream(dwgFile, FileMode.Open))
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
                    byte[] sendBytes = DwgUtils.GetSendBuffer(headerBytes, bodyBytes);
                    client.Send(sendBytes, sendBytes.Length);
                }
            }

            client.OnClose += Client_OnClose;
            client.OnConnect += Client_OnConnect;
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
            if (!Directory.Exists(dwgPath))
                Directory.CreateDirectory(dwgPath);
            zipFile = dwgPath + Path.GetFileNameWithoutExtension(dwgFile) + ".zip";
            try
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
                                using (var fs = new FileStream(zipFile, FileMode.Create))
                                {
                                    fs.Write(bytes, 0, bytes.Length);
                                    fs.Close();
                                }

                                required = pkgHeaderSize;

                                DwgUtils.UnZip(zipFile, dwgPath, "123456");
                                DwgUtils.renameFile(dwgPath);
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
                        if (File.Exists(dwgPath+Path.GetFileNameWithoutExtension(dwgFile)+"_G.json"))
                        {
                            insertData(dwgPath + Path.GetFileNameWithoutExtension(dwgFile) + "_G.json");
                            Console.WriteLine("succeed");
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        }

                    }
                }
                #endregion

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return HandleResult.Ok;
        }

        private static HandleResult Client_OnConnect(TcpClient sender)
        {
            return HandleResult.Ok;
        }

        private static HandleResult Client_OnClose(TcpClient sender, SocketOperation enOperation, int errorCode)
        {
            return HandleResult.Ok;
        }

        public static void insertData(string file)
        {
            StringBuilder sb = new StringBuilder();
            string ogr_style = string.Empty;
            string color = string.Empty;
            string angle = string.Empty;

            StreamReader sm = new StreamReader(file, Encoding.UTF8);
            string line;
            while ((line = sm.ReadLine()) != null)
            {
                if (line.Contains("Ogr_Style"))
                {
                    int i = line.IndexOf("\"Ogr_Style");
                    int j = line.IndexOf("\"geometry") - 3;
                    ogr_style = line.Substring(i, j - i);//ogr_style字段全内容

                    if (ogr_style.Contains("#"))//包含颜色值
                    {
                        color = ogr_style.Substring(ogr_style.IndexOf("#"), 7);
                    }
                    if (ogr_style.Contains("a:-"))
                    {
                        angle = ogr_style.Substring(ogr_style.IndexOf("a:") + 2, 4);
                    }
                    if (ogr_style.Contains("a:") && !line.Contains("a:-"))
                    {
                        angle = ogr_style.Substring(ogr_style.IndexOf("a:") + 2, 3);
                    }
                    string newProperty = string.Empty;
                    //angle
                    if (angle.Contains(","))
                        angle = angle.Replace(",", "");
                    if (angle.Contains("."))
                    {
                        if (angle.IndexOf(".") == angle.Length - 1)
                            angle = angle.Replace(".", "");
                    }
                    if (angle.Contains(")"))
                        angle = angle.Replace(")", "");
                    if (angle.Contains("\""))
                        angle = angle.Replace("\"", "");

                    //color
                    if (color == "#000000")
                        color = "#ffffff";
                    if (angle != "")
                        newProperty = "\"Color\":\"" + color + "\",\"Rotation\":" + angle;
                    else
                        newProperty = "\"Color\":\"" + color + "\"";

                    line = line.Replace(ogr_style, newProperty);

                    if (line.Contains("\"Layer\": \"河道中心线\""))
                    {
                        int m = line.IndexOf("geometry") - 4;
                        line = line.Insert(m, ",\"AutoLoad\": \"True\"");
                    }

                    if (line.Contains("\"Layer\": \"SL_桩号距离\""))
                    {
                        int m = line.IndexOf("geometry") - 4;
                        line = line.Insert(m, ",\"AutoLoad\": \"False\"");
                    }
                }

                sb.Append(line);
            }
            sm.Close();
            File.Delete(file);
            using (var fs = new FileStream(file, FileMode.Create))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                fs.Write(bytes, 0, bytes.Length);
                fs.Close();
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
