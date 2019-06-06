// (C) Copyright 2018 by  
//
using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Text;

// This line is not mandatory, but improves loading performances
[assembly: ExtensionApplication(typeof(DgnServer.MyPlugin))]

namespace DgnServer
{
    public class MyPlugin : IExtensionApplication
    {
        TcpPullServer server = new TcpPullServer();
        FileSystemWatcher watcher { get; set; }
        // 包头大小
        int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        PkgInfo pkgInfo = new PkgInfo();
        int id = 0;
        string path = @"E:\doc-fangfenglin\";
        int count = 0;
        string now { get; set; }
        string dgnFile { get; set; }
        string jpgFile { get; set; }
        string dwgFile { get; set; }
        IntPtr ID { get; set; }

        void IExtensionApplication.Initialize()
        {
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;
            server.IpAddress = "192.168.2.18";
            server.Port = 1138;
            server.Start();

            server.OnAccept += Server_OnAccept;
            server.OnReceive += Server_OnReceive;
            server.OnSend += Server_OnSend;
        }

        private HandleResult Server_OnSend(IntPtr connId, byte[] bytes)
        {

            return HandleResult.Ok;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (!File.Exists(dgnFile))
                return;
            try
            {
                Database db = new Database();
                db.SaveAs(dwgFile, DwgVersion.Newest);
                Document doc = Application.DocumentManager.Open(dwgFile, false);
                using (Transaction tan = doc.Database.TransactionManager.StartTransaction())
                {
                    doc.SendStringToExecute("-dgnattach\n" + dgnFile + "\n\n\n0,0,0\n\n\n_.ZOOM\nA\n-plot\nY\n\nPublishToWeb JPG.pc3\n\nL\n\n\n\nC\n\n\n\n\n" + jpgFile + "\n\n\nSaveAs\n\n"+dwgFile+"\nClose\n", false, false, false);
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
                watcher = new FileSystemWatcher();
                watcher.Path = path + now;
                watcher.Filter = "*.jpg";
                watcher.Created += Watcher_Created;
                watcher.EnableRaisingEvents = true;
            }

        }

        private HandleResult Server_OnReceive(IntPtr connId, int length)
        {
            if(count == 0)
                now = DateTime.Now.ToString("yyyyMMddhhmmss");
            if (!Directory.Exists(path + now))
            {
                Directory.CreateDirectory(path + now);
                dgnFile = path + now + "\\" + now + ".dgn";
                jpgFile = dgnFile.Replace(".dgn", ".jpg");
                dwgFile = dgnFile.Replace(".dgn", ".dwg");
            }

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
                    if (server.Fetch(ID,bufferPtr, required) == FetchResult.Ok)
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
                            using (var fs = new FileStream(dgnFile, FileMode.Create))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                                fs.Close();
                            }

                            required = pkgHeaderSize;
                        }

                        // 在后面赋值,因为前面需要用到pkgInfo.Length
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (server.SetExtra(ID,pkgInfo) == false)
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
            if(count == 0)
            {
                Application.Idle += Application_Idle;
                count = 1;
            }

            return HandleResult.Ok;
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(jpgFile))
                return;
            else
            {
                Thread.Sleep(5000);
                using (var fs = new FileStream(jpgFile, FileMode.Open))
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
            count = 0;
            watcher.Created -= Watcher_Created;
            watcher.Dispose();
            Utils.delDir(path + now);
        }

        private HandleResult Server_OnAccept(IntPtr connId, IntPtr pClient)
        {
            ID = connId;
            return HandleResult.Ok;
        }

        void IExtensionApplication.Terminate()
        {
            server.Stop();
            Application.Idle -= Application_Idle;
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
