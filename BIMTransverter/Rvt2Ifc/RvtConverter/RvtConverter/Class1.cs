using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;

namespace RvtConverter
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Class1 : IExternalApplication
    {
        private TcpPullServer server = new TcpPullServer();
        private UIApplication uiapp = null;
        private UIControlledApplication app { get; set; }

        private IntPtr ID { get; set; }
        public static string now = DateTime.Now.ToString("yyyyMMddhhmmss");
        public static string path = @"E:\doc-fangfenglin\"+now+"\\";
        private string rvtName = path + now + ".rvt";
        private string ifcName = now;
        private int count = 0;
        // 包头大小
        private static int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static PkgInfo pkgInfo = new PkgInfo();
        private static int id = 0;
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
       
        public Result OnStartup(UIControlledApplication application)
        {
            server.IpAddress = "192.168.2.18";server.Port = 1136;
            server.Start();
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;
            server.OnAccept += Server_OnAccept;
            server.OnClose += Server_OnClose;
            server.OnReceive += Server_OnReceive;
            server.OnSend += Server_OnSend;
            application.ControlledApplication.ApplicationInitialized += new EventHandler<Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs>(ControlledApplication_ApplicationInitialized);
            return Result.Succeeded;
        }

        //Revit初始化事件
        private void ControlledApplication_ApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            Autodesk.Revit.ApplicationServices.Application app = sender as Autodesk.Revit.ApplicationServices.Application;
            uiapp = new UIApplication(app);
            uiapp.Idling += Uiapp_Idling;
        }

        //Rvt导出为Ifc 事件
        private void Uiapp_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            if (count != 0)
                return;
            if (File.Exists(rvtName))
            {
                try
                {
                    Document doc = uiapp.Application.OpenDocumentFile(rvtName);
                    using (Transaction transaction = new Transaction(doc, "ifcexporter"))
                    {
                        transaction.Start();
                        IFCExportOptions opt = null;
                        doc.Export(path, ifcName, opt);
                        transaction.Commit();
                    }
                    doc.Close();
                    return;
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("error", ex.ToString());
                }
                finally
                {              
                    #region 发送生成的ifc
                    if (File.Exists(rvtName.Replace(".rvt", ".ifc")))
                    {
                        using (var fs = new FileStream(rvtName.Replace(".rvt",".ifc"), FileMode.Open))
                        {
                            // 封包体
                            byte[] bodyBytes = new byte[fs.Length];
                            fs.Read(bodyBytes, 0, bodyBytes.Length);
                            fs.Close();
                            // 封包头
                            PkgHeader header = new PkgHeader();
                            header.Id = ++id;
                            header.BodySize = bodyBytes.Length;
                            byte[] headerBytes = server.StructureToByte<PkgHeader>(header);


                            // 组合最终发送的封包 (封包头+封包体)
                            byte[] sendBytes = GetSendBuffer(headerBytes, bodyBytes);
                            server.Send(ID, sendBytes, sendBytes.Length);
                        }
                        delDir(path);//发送完成，删除文件
                    }
                    else
                    {
                        TaskDialog.Show("", rvtName.Replace(".rvt", ".ifc"));
                    }
                    #endregion
                }
                count++;
            }
        }

        private HandleResult Server_OnSend(IntPtr connId, byte[] bytes)
        {
            count = 0;
            return HandleResult.Ok;
        }

        private HandleResult Server_OnReceive(IntPtr connId, int length)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

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
                    if (server.Fetch(connId, bufferPtr, required) == FetchResult.Ok)
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
                            using (var fs = new FileStream(rvtName, FileMode.Create))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                                fs.Close();
                            }

                            required = pkgHeaderSize;
                        }

                        // 在后面赋值,因为前面需要用到pkgInfo.Length
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (server.SetExtra(connId, pkgInfo) == false)
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
          
            return HandleResult.Ok;
        }

        private HandleResult Server_OnClose(IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            return HandleResult.Ok;
        }

        private HandleResult Server_OnAccept(IntPtr connId, IntPtr pClient)
        {
            ID = connId;

            return HandleResult.Ok;
        }

        /// <summary>
        /// 删除文件夹及其子文件
        /// </summary>
        /// <param name="path"></param>
        private void delDir(string path)
        {
            foreach (string d in Directory.GetFileSystemEntries(path))
            {
                File.Delete(d);
            }
            Directory.Delete(path);
        }

        /// <summary>
        /// 封包
        /// </summary>
        /// <param name="headerBytes">包头</param>
        /// <param name="bodyBytes">包体</param>
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
