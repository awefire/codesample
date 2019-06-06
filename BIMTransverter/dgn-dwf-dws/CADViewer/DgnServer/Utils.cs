using AutoCAD;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DgnServer
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
        //Dwg转Jpg
        public static string Dwg2JPG(FileInfo file)
        {
            AcadApplication app = (AcadApplication)new AcadDocument().Application;
            AcadDocument doc = null;
            string destPath = string.Empty;

            try
            {
                doc = app.Documents.Application.ActiveDocument;
                doc.SetVariable("sdi", 0);
                doc.SetVariable("Filedia", 0);
                doc.SetVariable("BACKGROUNDPLOT", 0);
                doc.ActiveLayout.ConfigName = "PublishToWeb JPG.pc3";
                doc.ActiveLayout.UseStandardScale = true;
                doc.ActiveLayout.StandardScale = AutoCAD.AcPlotScale.acScaleToFit;
                doc.ActiveLayout.PlotType = AutoCAD.AcPlotType.acExtents;
                doc.ActiveLayout.CenterPlot = true;
                doc.ActiveLayout.PlotRotation = AutoCAD.AcPlotRotation.ac0degrees;
                doc.Plot.QuietErrorMode = true;
                destPath = Path.Combine(file.Directory.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".jpg");
                doc.Plot.PlotToFile(destPath);
                return destPath;
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                try
                {
                    doc.Close(false);
                    //app.Quit();
                }
                catch
                {

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
