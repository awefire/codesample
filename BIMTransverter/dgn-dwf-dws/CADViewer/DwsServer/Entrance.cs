// (C) Copyright 2018 by  
//
using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

// This line is not mandatory, but improves loading performances
[assembly: ExtensionApplication(typeof(DwsServer.Entrance))]

namespace DwsServer
{
    public class Entrance : IExtensionApplication
    {
        TcpPullServer server = new TcpPullServer();
        FileSystemWatcher watcher { get; set; }
        // 包头大小
        int pkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        PkgInfo pkgInfo = new PkgInfo();
        int id = 0;
        string dwsFile { get; set; }
        string jpgFile { get; set; }
        IntPtr ID { get; set; }
        string now { get; set; }
        int step = 0;
        string path = @"E:\doc-fangfenglin\";
        void IExtensionApplication.Initialize()
        {
            server.IpAddress = "192.168.2.18";
            server.Port = 1140;
            pkgInfo.IsHeader = true;
            pkgInfo.Length = pkgHeaderSize;
            server.Start();
            server.OnAccept += Server_OnAccept;
            server.OnReceive += Server_OnReceive;
            server.OnSend += Server_OnSend;
        }

        private HandleResult Server_OnSend(IntPtr connId, byte[] bytes)
        {
            return HandleResult.Ok;
        }

        private HandleResult Server_OnReceive(IntPtr connId, int length)
        {
            if(step == 0)
            {
                now = DateTime.Now.ToString("yyyyMMddhhmmss");
                if(!Directory.Exists(path + now))
                {
                    Directory.CreateDirectory(path + now);
                    dwsFile = path + now + "\\" + now + ".dws";
                    jpgFile = dwsFile.Replace(".dws", ".jpg");
                }
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
                    if (server.Fetch(ID, bufferPtr, required) == FetchResult.Ok)
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
                            using (var fs = new FileStream(dwsFile, FileMode.Create))
                            {
                                fs.Write(bytes, 0, bytes.Length);
                                fs.Close();
                            }

                            required = pkgHeaderSize;
                            Application.Idle += Application_Idle;//注册CAD事件  
                        }

                        // 在后面赋值,因为前面需要用到pkgInfo.Length
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (server.SetExtra(ID, pkgInfo) == false)
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

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(jpgFile))
                return;
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
                byte[] sendBytes = GetSendBuffer(headerBytes, bodyBytes);
                server.Send(ID, sendBytes, sendBytes.Length);
            }
            step = 0;
            watcher.Created -= Watcher_Created;
            watcher.Dispose();
            delDir(path + now);
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (!File.Exists(dwsFile))
                return;
            try
            {
                Document sdoc = Application.DocumentManager.Open(dwsFile, false);
                sdoc.CloseAndSave(dwsFile.Replace(".dws",".dwg"));
                Document gdoc = Application.DocumentManager.Open(dwsFile, false);
                using (Transaction tan = gdoc.Database.TransactionManager.StartTransaction())
                {
                    gdoc.SendStringToExecute("ZOOM\nA\n-plot\nY\n\nPublishToWeb JPG.pc3\n\nL\n\n\n\nC\n\n\n\n\n" + jpgFile + "\n\n\nSaveAs\n\n" + dwsFile.Replace(".dws", ".dwg") + "\nClose\n", false, false, false);
                    tan.Commit();
                }
                Application.Idle -= Application_Idle;
            }
            catch(Autodesk.AutoCAD.Runtime.Exception ex)
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

        private HandleResult Server_OnAccept(IntPtr connId, IntPtr pClient)
        {
            ID = connId;
            return HandleResult.Ok;
        }

        void IExtensionApplication.Terminate()
        {

        }
        /// <summary>
        /// 封包
        /// </summary>
        /// <param name="headerBytes"></param>
        /// <param name="bodyBytes"></param>
        /// <returns></returns>
        private byte[] GetSendBuffer(byte[] headerBytes, byte[] bodyBytes)
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
        private void delDir(string path)
        {
            foreach (string d in Directory.GetFileSystemEntries(path))
            {
                File.Delete(d);
            }
            Directory.Delete(path);
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
