using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BimJson
{
    class Utils
    {
        [DllImport("IfcConvert.dll", EntryPoint = "getdata", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern void exportxml(ref byte inputifc, ref byte outputxml);
        /// <summary>
        /// XML转化成json
        /// </summary>
        /// <param name="inputxml">输入xml文件(全路径)</param>
        /// <param name="outputjson">输出json文件(全路径)</param>
        public static void ConvertXML(string inputxml,string outputjson)
        {
            if (!File.Exists(inputxml))
                return;
            string xml;
            using (var inputFileStream = File.OpenRead(inputxml))
            {
                using (var inputStreamReader = new StreamReader(inputFileStream))
                {
                    xml = inputStreamReader.ReadToEnd();
                }
            }
            var json = Converter.ConvertToJson(xml);

            using (var outputFileStream = File.Open(outputjson, FileMode.Create, FileAccess.Write))
            {
                using (var outputStreamWriter = new StreamWriter(outputFileStream))
                {
                    outputStreamWriter.Write(json);
                    outputStreamWriter.Flush();
                    outputStreamWriter.Close();
                }
            }

            File.Delete(inputxml);
        }

        /// <summary>
        /// 在程序窗口中调用cmd命令行
        /// </summary>
        public static void CmdCommand(string fileName)
        {
            string str3 = "cd /d " + Environment.CurrentDirectory;
            string str1 = "IfcConvert.exe " + fileName+".ifc " + fileName + "_P.xml";
            string str2 = "IfcConvert.exe " + fileName + ".ifc " + fileName + "_T.xml";
            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.UseShellExecute = false;    //是否使用操作系统shell启动
            p.StartInfo.RedirectStandardInput = true;//接受来自调用程序的输入信息
            p.StartInfo.RedirectStandardOutput = true;//由调用程序获取输出信息
            p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
            p.StartInfo.CreateNoWindow = true;//不显示程序窗口
            p.Start();//启动程序

            //向cmd窗口发送输入信息
            p.StandardInput.WriteLine(str3);
            p.StandardInput.WriteLine(str1 + "&exit");
            p.StandardInput.WriteLine(str2 + "&exit");
            p.StandardInput.AutoFlush = true;

            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
        }

        /// <summary>
        /// 写入命令脚本bat
        /// </summary>
        /// <param name="fileName">bat生成路径</param>
        public static void writeBATFile(string fileNme)
        {
            string fileContent;
            string filePath = Environment.CurrentDirectory + @"\" + fileNme + ".bat";
            fileContent = "@echo off"+"\r\n";
            fileContent += "cd /d " + Environment.CurrentDirectory + "\r\n";
            fileContent += "IfcConvert.exe " + fileNme + ".ifc " + fileNme + "_P.xml" + "\r\n";
            fileContent += "IfcConvert.exe " + fileNme + ".ifc " + fileNme + "_T.xml";
            if (!File.Exists(filePath))
            {
                FileStream fs1 = new FileStream(filePath, FileMode.Create, FileAccess.Write);//创建写入文件
                StreamWriter sw = new StreamWriter(fs1);
                sw.WriteLine(fileContent);//开始写入值
                sw.Close();
                fs1.Close();
            }
            else
            {
                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Write);
                StreamWriter sr = new StreamWriter(fs);
                sr.WriteLine(fileContent);//开始写入值
                sr.Close();
                fs.Close();
            }
        }

        /// <summary>
        /// 运行bat
        /// </summary>
        /// <param name="batName"></param>
        /// <param name="filePath"></param>
        public static void RunBat(string batName,string filePath)
        {
            Console.WriteLine("正在解析文件...");
            Process proc = null;
            try
            {
                string targetDir = string.Format(filePath);//this is where testChange.bat lies
                proc = new Process();
                proc.StartInfo.WorkingDirectory = targetDir;
                proc.StartInfo.FileName = batName + ".bat";
                proc.StartInfo.Arguments = string.Format("10");//this is argument
                proc.StartInfo.CreateNoWindow = true;
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;//这里设置DOS窗口不显示，经实践可行
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Occurred :{0},{1}", ex.Message, ex.StackTrace.ToString());
            }
        }

        public static void exportXML()
        {
            Console.WriteLine("->正在解析属性及结构树数据...");
            string str1 = Program.filePath;
            string str2 = Program.filePath.Remove(Program.filePath.IndexOf("."),4)+"_P.xml";

            byte[] file1 = new byte[1000];
            byte[] file2 = new byte[1000];

            for (int i = 0; i < str1.Length; i++)
            {
                file1[i] = (byte)str1[i];
            }
            for (int i = 0; i < str2.Length; i++)
            {
                file2[i] = (byte)str2[i];
            }
            string str3 = Program.filePath;
            string str4 = Program.filePath.Remove(Program.filePath.IndexOf("."), 4) + "_T.xml";

            byte[] file3 = new byte[1000];
            byte[] file4 = new byte[1000];

            for (int i = 0; i < str3.Length; i++)
            {
                file3[i] = (byte)str3[i];
            }
            for (int i = 0; i < str4.Length; i++)
            {
                file4[i] = (byte)str4[i];
            }
            exportxml(ref file1[0], ref file2[0]);
            FileInfo info1 = new FileInfo(str2);
            if (info1.Exists)
            {
                info1.Attributes = FileAttributes.Hidden;
                Console.WriteLine("succeed");
            }
            exportxml(ref file3[0], ref file4[0]);
            FileInfo info2 = new FileInfo(str4);
            if (info1.Exists)
            {
                info2.Attributes = FileAttributes.Hidden;
                Console.WriteLine("succeed");
            }
        }
    }
}
