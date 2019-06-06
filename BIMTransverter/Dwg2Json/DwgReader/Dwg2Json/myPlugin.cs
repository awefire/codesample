// (C) Copyright 2018 by  
//
using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using HPSocketCS;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

// This line is not mandatory, but improves loading performances
[assembly: ExtensionApplication(typeof(Dwg2Json.MyPlugin))]

namespace Dwg2Json
{
    public class MyPlugin : IExtensionApplication
    {
        public static HPSocketCS.TcpPullServer<ClientInfo> server = new HPSocketCS.TcpPullServer<ClientInfo>();
        // 包头大小
        public static int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        public static string now = DateTime.Now.ToString("yyyyMMddhhmmss");//获取程序运行当前时间
        public static string listPath = "E:\\doc-fangfenglin\\" + now;//创建临时文件夹
        public static string dwgFile = listPath + "\\" + now + ".dwg";
        public static string dxfFile = listPath + "\\" + now + ".dxf";
        public static IntPtr ID { get; set; }
        public static int id = 0;
        public static int count = 0;//用于判断是否接受完数据
        void IExtensionApplication.Initialize()
        {
            server.IpAddress = "192.168.2.18";
            server.Port = 1137;
            server.Start();

            server.OnAccept += Server_OnAccept;
            server.OnClose += Server_OnClose;
            server.OnReceive += Server_OnReceive;
            server.OnSend += Server_OnSend;
        }

        private HandleResult Server_OnSend(IntPtr connId, byte[] bytes)
        {
            return HandleResult.Ok;
        }

        private HandleResult Server_OnReceive(IntPtr connId, int length)
        {
            if (!Directory.Exists(listPath))//文件夹不存在则创建
                Directory.CreateDirectory(listPath);

            #region 写文件
            // 数据到达了
            // clientInfo 就是accept里传入的附加数据了
            var clientInfo = server.GetExtra(connId);
            if (clientInfo == null)
            {
                return HandleResult.Error;
            }

            PkgInfo pkgInfo = clientInfo.PkgInfo;

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
                    bufferPtr = Marshal.AllocHGlobal(required); ;
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
                            var fs = new FileStream(dwgFile, FileMode.Create);
                            fs.Write(bytes, 0, bytes.Length);
                            fs.Close();

                            required = pkgHeaderSize;
                        }

                        // 在后面赋值,因为前面需要用到pkgInfo.Length
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (server.SetExtra(connId, clientInfo) == false)
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
                #endregion
            }
            if(File.Exists(dwgFile) && count == 0)
                Application.Idle += Application_Idle;
          

            return HandleResult.Ok;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (count != 0)
                return;
            try
            {
                Document doc = Application.DocumentManager.Open(dwgFile, true);
                Database db = doc.Database;
                Autodesk.AutoCAD.DatabaseServices.TransactionManager tm = db.TransactionManager;
                using (Transaction tan = tm.StartTransaction())
                {
                    db.DxfOut(dxfFile, 16, DwgVersion.Newest);

                    Utils.CmdCommand(dwgFile.Replace(".dwg", ""));
                    Utils.GetDwgProperty(dwgFile.Replace(".dwg", ""));
                    Utils.ReadGeometry(dwgFile.Replace(".dwg", ""));
                    Utils.TreeToJson(dwgFile.Replace(".dwg","")+"_T.json");
                    Utils.PropertyToJson(dwgFile.Replace(".dwg", "") + "_P.json");
                    Utils.EntityColor.Clear();
                    Utils.EntityHandle.Clear();
                    Utils.LayerColor.Clear();
                    Utils.LayerName.Clear();
                    Utils.Text.Clear();
                    Utils.TextRotation.Clear();
                    tan.Commit();
                }
                Application.Idle -= Application_Idle;

            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                Application.ShowAlertDialog(ex.ToString());
            }
            finally
            {
                try
                {
                    Application.DocumentManager.CloseAll();
                    //Thread th1 = new Thread(Utils.delFile);
                    //th1.Start();
                    //Thread.Sleep(3000);
                    File.Delete(dxfFile);


                    Utils.zipFile(listPath, "E:\\doc-fangfenglin\\" + now + ".zip");
                    using (var fs = new FileStream("E:\\doc-fangfenglin\\" + now + ".zip", FileMode.Open))
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
                        byte[] sendBytes = Utils.GetSendBuffer(headerBytes, bodyBytes);
                        server.Send(ID, sendBytes, sendBytes.Length);
                    }
                }
                catch (System.Exception ex)
                {
                    Application.ShowAlertDialog(ex.ToString());
                }
                count++;
            }
        }

        private HandleResult Server_OnClose(IntPtr connId, SocketOperation enOperation, int errorCode)
        {
            count = 0;
            try
            {
                File.Delete("E:\\doc-fangfenglin\\" + now + ".zip");
                Utils.delDir(listPath);
            }catch{
                Thread.Sleep(8000);
                Thread th = new Thread(Utils.delFile);
                th.Start();
            }
            return HandleResult.Ok;
        }

        private HandleResult Server_OnAccept(IntPtr connId, IntPtr pClient)
        {
            ID = connId;
            // 设置附加数据
            ClientInfo clientInfo = new ClientInfo();
            clientInfo.ConnId = connId;
            clientInfo.IpAddress = "192.168.2.18";
            clientInfo.Port = 1131;
            clientInfo.PkgInfo = new PkgInfo()
            {
                IsHeader = true,
                Length = pkgHeaderSize,
            };
            if (server.SetExtra(connId, clientInfo) == false)
            {
                Console.WriteLine(string.Format(" > [{0},OnAccept] -> SetConnectionExtra fail", connId));
            }
            return HandleResult.Ok;
        }

        void IExtensionApplication.Terminate()
        {

        }

    }

    [StructLayout(LayoutKind.Sequential)]
    public class ClientInfo
    {
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
        public PkgInfo PkgInfo { get; set; }
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
