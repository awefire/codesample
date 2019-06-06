using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.GZip;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using System.Collections;
using System.Threading;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Geometry;

namespace Dwg2Json
{
    class Utils
    {
        public static ArrayList LayerName = new ArrayList();//图层名称
        public static ArrayList LayerColor = new ArrayList();//图层颜色
        public static ArrayList EntityType = new ArrayList();//实体类型
        public static ArrayList EntityHandle = new ArrayList();//实体句柄
        public static ArrayList LineType = new ArrayList();//线型
        public static ArrayList EntityColor = new ArrayList();//实体颜色
        public static ArrayList Coordinate = new ArrayList();//坐标
        public static ArrayList Text = new ArrayList();//文字内容
        public static ArrayList Height = new ArrayList();//文字高度
        public static ArrayList LineWidth = new ArrayList();//线宽
        public static ArrayList EntityLayer = new ArrayList();//实体所属图层
        public static ArrayList CRadius = new ArrayList();//半径
        public static ArrayList ExtenedEntity = new ArrayList();//附加信息
        public static ArrayList TextRotation = new ArrayList();//文字矩阵
        public static string _position = string.Empty;//图幅位置

        /// <summary>
        /// 压缩文件夹
        /// </summary>
        /// <param name="fileStr">源文件夹</param>
        /// <param name="zipStr">目标压缩文件名</param>
        public static void zipFile(string fileStr,string zipStr)
        {
            if (Directory.Exists(fileStr))
            {
                if (fileStr[fileStr.Length - 1] != Path.DirectorySeparatorChar)
                    fileStr += Path.DirectorySeparatorChar;
                ZipOutputStream s = new ZipOutputStream(File.Create(zipStr));
                s.SetLevel(6);
                zip(fileStr,s,fileStr);
                s.Finish();
                s.Close();
            }
        }
        private static void zip(string strFile, ZipOutputStream s, string staticFile)
        {
            if (strFile[strFile.Length - 1] != Path.DirectorySeparatorChar) strFile += Path.DirectorySeparatorChar;
            Crc32 crc = new Crc32();
            string[] filenames = Directory.GetFileSystemEntries(strFile);
            foreach (string file in filenames)
            {

                if (Directory.Exists(file))
                {
                    zip(file, s, staticFile);
                }

                else // 否则直接压缩文件
                {
                    //打开压缩文件
                    FileStream fs = File.OpenRead(file);

                    byte[] buffer = new byte[fs.Length];
                    fs.Read(buffer, 0, buffer.Length);
                    string tempfile = file.Substring(staticFile.LastIndexOf("\\") + 1);
                    ZipEntry entry = new ZipEntry(tempfile);

                    entry.DateTime = DateTime.Now;
                    entry.Size = fs.Length;
                    fs.Close();
                    crc.Reset();
                    crc.Update(buffer);
                    entry.Crc = crc.Value;
                    s.PutNextEntry(entry);

                    s.Write(buffer, 0, buffer.Length);
                }
            }
        }

        /// <summary>
        /// 解压文件夹
        /// </summary>
        /// <param name="TargetFile">zip文件</param>
        /// <param name="fileDir">解压目录</param>
        /// <returns></returns>
        public string unZipFile(string TargetFile, string fileDir)
        {
            string rootFile = " ";
            try
            {
                //读取压缩文件(zip文件),准备解压缩
                ZipInputStream s = new ZipInputStream(File.OpenRead(TargetFile.Trim()));
                ZipEntry theEntry;
                string path = fileDir;
                //解压出来的文件保存的路径

                string rootDir = " ";
                //根目录下的第一个子文件夹的名称
                while ((theEntry = s.GetNextEntry()) != null)
                {
                    rootDir = Path.GetDirectoryName(theEntry.Name);
                    //得到根目录下的第一级子文件夹的名称
                    if (rootDir.IndexOf("\\") >= 0)
                    {
                        rootDir = rootDir.Substring(0, rootDir.IndexOf("\\") + 1);
                    }
                    string dir = Path.GetDirectoryName(theEntry.Name);
                    //根目录下的第一级子文件夹的下的文件夹的名称
                    string fileName = Path.GetFileName(theEntry.Name);
                    //根目录下的文件名称
                    if (dir != " ")
                    //创建根目录下的子文件夹,不限制级别
                    {
                        if (!Directory.Exists(fileDir + "\\" + dir))
                        {
                            path = fileDir + "\\" + dir;
                            //在指定的路径创建文件夹
                            Directory.CreateDirectory(path);
                        }
                    }
                    else if (dir == " " && fileName != "")
                    //根目录下的文件
                    {
                        path = fileDir;
                        rootFile = fileName;
                    }
                    else if (dir != " " && fileName != "")
                    //根目录下的第一级子文件夹下的文件
                    {
                        if (dir.IndexOf("\\") > 0)
                        //指定文件保存的路径
                        {
                            path = fileDir + "\\" + dir;
                        }
                    }

                    if (dir == rootDir)
                    //判断是不是需要保存在根目录下的文件
                    {
                        path = fileDir + "\\" + rootDir;
                    }

                    //以下为解压缩zip文件的基本步骤
                    //基本思路就是遍历压缩文件里的所有文件,创建一个相同的文件。
                    if (fileName != String.Empty)
                    {
                        FileStream streamWriter = File.Create(path + "\\" + fileName);

                        int size = 2048;
                        byte[] data = new byte[2048];
                        while (true)
                        {
                            size = s.Read(data, 0, data.Length);
                            if (size > 0)
                            {
                                streamWriter.Write(data, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }

                        streamWriter.Close();
                    }
                }
                s.Close();

                return rootFile;
            }
            catch (Exception ex)
            {
                return "1; " + ex.Message;
            }
        }

        /// <summary>
        /// 读取layer--layercolor信息
        /// </summary>
        /// <param name="dwgfile"></param>
        public static void GetDwgProperty(string dwgfile)
        {
            Database db = new Database(false, true);
            if (File.Exists(dwgfile + ".dwg"))
            {
                try
                {
                    db.ReadDwgFile(dwgfile + ".dwg", FileShare.Read, true, null);

                    using (Transaction trans = db.TransactionManager.StartTransaction())
                    {

                        LayerTable lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                        foreach (ObjectId id in lt)
                        {
                            LayerTableRecord ltr = (LayerTableRecord)trans.GetObject(id, OpenMode.ForRead);
                            if (ltr != null)
                            {
                                LayerName.Add(ltr.Name);
                                int a = ltr.Color.ColorValue.A;
                                int r = ltr.Color.ColorValue.R;
                                int g = ltr.Color.ColorValue.G;
                                int b = ltr.Color.ColorValue.B;
                                LayerColor.Add(RgbToHex(a, r, g, b));

                            }
                        }
                        trans.Commit();

                    }
                }catch(Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    Application.ShowAlertDialog(ex.ToString());
                }

                string str = dwgfile+"_L.json";
                PropertyJson(str);

            }
        }
        /// <summary>
        /// 输出json文件
        /// </summary>
        /// <param name="filename"></param>
        private static void PropertyJson(string filename)
        {
            string jsonstr = "[";
            StringBuilder build = new StringBuilder();
            for (int i = 0; i < LayerColor.Count; i++)
            {
                if (i == LayerColor.Count - 1)
                    build.Append("{\"Name\":" + "\"" + LayerName[i] + "\"" + ",\"Color\":" + "\"" + LayerColor[i] + "\"" + "}");
                else
                    build.Append("{\"Name\":" + "\"" + LayerName[i] + "\"" + ",\"Color\":" + "\"" + LayerColor[i] + "\"" + "},");
            }
            jsonstr += build.ToString();
            jsonstr += "]";

            FileStream fs = new FileStream(filename, FileMode.Create);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(jsonstr);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
        }

        private static string RgbToHex(int a, int r, int g, int b)
        {
            string hex = System.Drawing.ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(a, r, g, b));
            return hex;
        }

        /// <summary>
        /// dxf转geojson
        /// </summary>
        /// <param name="fileName">dxf文件</param>
        ///            -sql \"select layer,subclasses,entityhandle,ogr_style from entities\" 
        public static void CmdCommand(string fileName)
        {
            string str3 = "cd /d E:\\doc-fangfenglin";
            string str1 = "dwg2json.exe -f geojson -sql \"select Layer,SubClasses,EntityHandle,Linetype,ExtendedEntity,Text,Ogr_Style from entities\" " + fileName + "_G.json " + fileName + ".dxf";
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
            p.StandardInput.AutoFlush = true;

            p.WaitForExit();//等待程序执行完退出进程
            p.Close();
        }

        public static void ReadGeometry(string cfile)
        {
            if (LayerName.Count == 0)
                Application.ShowAlertDialog("\nfailed");
            else
            {
                for (int i = 0; i < LayerName.Count; i++)
                {
                    GetDwgGeometry(LayerName[i].ToString());
                }
                //ColorToJson(cfile+"_A.json");
            }
        }
        private static void ColorToJson(string filename)
        {
            string jsonstr1 = "[";
            StringBuilder build1 = new StringBuilder();
            if (EntityColor.Count == 0)
                Application.ShowAlertDialog("failed");
            for (int i = 0; i < EntityColor.Count; i++)
            {
                if (i == EntityColor.Count - 1)
                {
                    if(TextRotation[i].ToString() == "null")
                        build1.Append("{\"EntityHandle\":\"" + EntityHandle[i] + "\",\"Color\":\"" + EntityColor[i] + "\"}");
                    else
                    {
                        build1.Append("{\"EntityHandle\":\"" + EntityHandle[i] + "\",\"Color\":\"" + EntityColor[i] + "\",\"Rotation\":" + TextRotation[i] + "}");
                    }
                 
                }
                else
                {
                    if (TextRotation[i].ToString() == "null")
                        build1.Append("{\"EntityHandle\":\"" + EntityHandle[i] + "\",\"Color\":\"" + EntityColor[i] + "\"},");
                    else
                    {
                        build1.Append("{\"EntityHandle\":\"" + EntityHandle[i] + "\",\"Color\":\"" + EntityColor[i] + "\",\"Rotation\":" + TextRotation[i] + "},");
                    }
                       
                }           
            }
            jsonstr1 += build1.ToString();
            jsonstr1 += "]";

            FileStream fs = new FileStream(filename, FileMode.Create);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(jsonstr1);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
        }

        public static void PropertyToJson(string filename)
        {
            if (Text.Count == 0)
                return;
            int layercount = LayerName.Count;
            string tuhao = string.Empty;
            string gongcheng = string.Empty;
            string tuming = string.Empty;
            string zhuanghao = string.Empty;
            string location = _position;
            foreach (string text in Text)
            {
                if (text.Contains("S2011"))
                    tuhao = text;
                if (text.Contains("上海市2012年"))
                    gongcheng = text;
                if (text.Contains("型护岸"))
                    tuming = text;
                if (text.Contains("桩号范围"))
                {
                    int i = text.IndexOf("围") + 1;
                    int j = text.IndexOf("内");
                    int len = j - i;
                    zhuanghao = text.Substring(i, len);
                }
            }
            if (tuhao == string.Empty)
                tuhao = "null";
            if (gongcheng == string.Empty)
                gongcheng = "null";
            if (tuming == string.Empty)
                tuming = "null";
            if (zhuanghao == string.Empty)
                zhuanghao = "null";

            string property = "{\"tuhao\":\"" + tuhao + "\",\"gongcheng\":\"" + gongcheng + "\",\"zhuanghao\":\""+zhuanghao+"\",\"tuming\":\"" + tuming + "\",\"tucengzongshu\":" + layercount + ",\"zuoluo\":\"" + location + "\"}";
            using(var fs = new FileStream(filename, FileMode.Create))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(property);
                fs.Write(bytes,0,bytes.Length);
                fs.Close();
            }
        }
        public static void TreeToJson(string filename)
        {
            if (Text.Count == 0)
                return;
            string tuhao = string.Empty;
            string type = string.Empty;
            string name = string.Empty;
            foreach(string text in Text)
            {
                if (text.Contains("S2011"))
                    tuhao = text;
                if (text.Contains("所属类别：A1"))
                    type = "A1";
                if (text.Contains("所属类别;A2"))
                    type = "A2";
                if (text.Contains("型护岸"))
                    name = text;
            }
            if (tuhao == string.Empty)
                tuhao = "null";
            if (type == string.Empty)
                type = "其他";
            if (name == string.Empty)
                name = "无";

            string tree = string.Empty;
            tree = "{\"tuhao\":\"" + tuhao + "\",\"leibie\":\"" + type + "\",\"tuming\":\"" + name + "\"}";
            using(var fs = new FileStream(filename, FileMode.Create))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(tree);
                fs.Write(bytes, 0, bytes.Length);
                fs.Close();
            }
        }

        /// <summary>
        /// 读取dwg中entities信息
        /// </summary>
        public static void GetDwgGeometry(string layerName) 
        {
            Database db = HostApplicationServices.WorkingDatabase;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(layerName))
                {
                    BlockTableRecord ltr = (BlockTableRecord)trans.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    int a, r, g, b = 0;
                    foreach (ObjectId objId in ltr)
                    {
                        Entity ent = (Entity)trans.GetObject(objId, OpenMode.ForRead);

                        #region 遍历entities
                        if (ent.Layer == layerName)
                        {
                            string type = ent.GetType().ToString().Replace("Autodesk.AutoCAD.DatabaseServices.", "");
                            if (type == "Line")
                            {
                                Line line = (Line)ent;
                                //EntityLayer.Add(line.Layer);
                                //EntityType.Add(type);
                                //LineType.Add(line.Linetype);
                                a = line.Color.ColorValue.A;
                                r = line.Color.ColorValue.R;
                                g = line.Color.ColorValue.G;
                                b = line.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                EntityHandle.Add(line.Handle);
                                TextRotation.Add("null");
                                //Coordinate.Add(line.StartPoint + "," + line.EndPoint);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add(line.LineWeight);
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "AttributeDefinition")
                            {
                                AttributeDefinition attribute = (AttributeDefinition)ent;
                                //EntityLayer.Add(attribute.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(attribute.Handle);
                                //LineType.Add("null");
                                a = attribute.Color.ColorValue.A;
                                r = attribute.Color.ColorValue.R;
                                g = attribute.Color.ColorValue.G;
                                b = attribute.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(attribute.Position);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(attribute.ExtensionDictionary);
                            }
                            else if (type == "DBPoint")
                            {
                                DBPoint dbpoint = (DBPoint)ent;
                                //EntityLayer.Add(dbpoint.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(dbpoint.Handle);
                                //LineType.Add(dbpoint.Linetype);
                                a = dbpoint.Color.ColorValue.A;
                                r = dbpoint.Color.ColorValue.R;
                                g = dbpoint.Color.ColorValue.G;
                                b = dbpoint.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(dbpoint.Position);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "Ellipse")
                            {
                                Ellipse ellipse = (Ellipse)ent;
                                //EntityLayer.Add(ellipse.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(ellipse.Handle);
                                //LineType.Add("null");
                                a = ellipse.Color.ColorValue.A;
                                r = ellipse.Color.ColorValue.R;
                                g = ellipse.Color.ColorValue.G;
                                b = ellipse.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(ellipse.GetPlane().GetCoordinateSystem());
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "DBText")
                            {
                                DBText dbtext = (DBText)ent;
                                //EntityLayer.Add(dbtext.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(dbtext.Handle);
                                TextRotation.Add(dbtext.Rotation);
                                //LineType.Add("null");
                                a = dbtext.Color.ColorValue.A;
                                r = dbtext.Color.ColorValue.R;
                                g = dbtext.Color.ColorValue.G;
                                b = dbtext.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                //Coordinate.Add(dbtext.Position);
                                Text.Add(dbtext.TextString);
                                //Height.Add(dbtext.Height);
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                                if (dbtext.TextString.Contains("说明"))
                                    _position = dbtext.Position.ToString();
                            }
                            else if (type == "RadialDimension")
                            {
                                RadialDimension radial = (RadialDimension)ent;
                                //EntityLayer.Add(radial.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(radial.Handle);
                                //LineType.Add("null");
                                a = radial.Color.ColorValue.A;
                                r = radial.Color.ColorValue.R;
                                g = radial.Color.ColorValue.G;
                                b = radial.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(radial.GetPlane().GetCoordinateSystem());
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(radial.ExtensionDictionary);
                            }
                            else if (type == "AlignedDimension")
                            {
                                AlignedDimension aligned = (AlignedDimension)ent;
                                //EntityLayer.Add(aligned.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(aligned.Handle);
                                //LineType.Add("null");
                                a = aligned.Color.ColorValue.A;
                                r = aligned.Color.ColorValue.R;
                                g = aligned.Color.ColorValue.G;
                                b = aligned.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(aligned.GeometricExtents);
                                //Text.Add(aligned.TextAttachment);
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "Spline")
                            {
                                Spline spline = (Spline)ent;
                                //EntityLayer.Add(spline.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(spline.Handle);
                                //LineType.Add(spline.Linetype);
                                a = spline.Color.ColorValue.A;
                                r = spline.Color.ColorValue.R;
                                g = spline.Color.ColorValue.G;
                                b = spline.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //List<Point3d> points_spline = new List<Point3d>();
                                //for(int i = 0; i < spline.NumControlPoints; i++)
                                //{
                                //    points_spline.Add(spline.GetControlPointAt(i));
                                //}
                                //Coordinate.Add(points_spline);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add(spline.LineWeight);
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "RotatedDimension")
                            {
                                RotatedDimension rotate = (RotatedDimension)ent;
                                //EntityLayer.Add(rotate.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(rotate.Handle);
                                //LineType.Add("null");
                                a = rotate.Color.ColorValue.A;
                                r = rotate.Color.ColorValue.R;
                                g = rotate.Color.ColorValue.G;
                                b = rotate.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(rotate.GeometricExtents);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(rotate.ExtensionDictionary);
                            }
                            else if (type == "Polyline")
                            {
                                Polyline pline = (Polyline)ent;
                                //EntityLayer.Add(pline.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(pline.Handle);
                                //LineType.Add(pline.Linetype);
                                a = pline.Color.ColorValue.A;
                                r = pline.Color.ColorValue.R;
                                g = pline.Color.ColorValue.G;
                                b = pline.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //CRadius.Add("null");
                                //List<Point3d> points_polyline = new List<Point3d>();
                                //for(int i = 0; i < pline.NumberOfVertices; i++)
                                //{
                                //    points_polyline.Add(pline.GetPoint3dAt(i));
                                //}
                                //Coordinate.Add(points_polyline);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add(pline.LineWeight);
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "MText")
                            {
                                MText mtext = (MText)ent;
                                //EntityLayer.Add(mtext.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(mtext.Handle);
                                TextRotation.Add(mtext.Rotation);
                                //LineType.Add("null");
                                a = mtext.Color.ColorValue.A;
                                r = mtext.Color.ColorValue.R;
                                g = mtext.Color.ColorValue.G;
                                b = mtext.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                //Coordinate.Add(mtext.Location);
                                //Text.Add(mtext.Contents);
                                //Height.Add(mtext.Height);
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                                if (mtext.Text.Contains("说明"))
                                    _position = mtext.Location.ToString();
                            }
                            else if (type == "Arc")
                            {
                                Arc arc = (Arc)ent;
                                //EntityLayer.Add(arc.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(arc.Handle);
                                //LineType.Add("null");
                                a = arc.Color.ColorValue.A;
                                r = arc.Color.ColorValue.R;
                                g = arc.Color.ColorValue.G;
                                b = arc.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(arc.Center);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add(arc.Radius);
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "Hatch")
                            {
                                Hatch hatch = (Hatch)ent;
                                //EntityLayer.Add(hatch.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(hatch.Handle);
                                //LineType.Add("null");
                                a = hatch.Color.ColorValue.A;
                                r = hatch.Color.ColorValue.R;
                                g = hatch.Color.ColorValue.G;
                                b = hatch.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(hatch.GeometricExtents);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(hatch.ExtensionDictionary);
                            }
                            else if (type == "Dimension")
                            {
                                Dimension dim = (Dimension)ent;
                                //EntityLayer.Add(dim.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(dim.Handle);
                                //LineType.Add("null");
                                a = dim.Color.ColorValue.A;
                                r = dim.Color.ColorValue.R;
                                g = dim.Color.ColorValue.G;
                                b = dim.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(dim.TextPosition);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(dim.ExtensionDictionary);
                            }
                            else if (type == "Circle")
                            {
                                Circle circle = (Circle)ent;
                                //EntityLayer.Add(circle.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(circle.Handle);
                                //LineType.Add("null");
                                a = circle.Color.ColorValue.A;
                                r = circle.Color.ColorValue.R;
                                g = circle.Color.ColorValue.G;
                                b = circle.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(circle.Center);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add(circle.Radius);
                                //ExtenedEntity.Add("null");
                            }
                            else if (type == "BlockReference")
                            {
                                BlockReference block = (BlockReference)ent;
                                //EntityLayer.Add(block.Layer);
                                //EntityType.Add(type);
                                EntityHandle.Add(block.Handle);
                                //LineType.Add("null");
                                a = block.Color.ColorValue.A;
                                r = block.Color.ColorValue.R;
                                g = block.Color.ColorValue.G;
                                b = block.Color.ColorValue.B;
                                EntityColor.Add(RgbToHex(a, r, g, b));
                                TextRotation.Add("null");
                                //Coordinate.Add(block.GeometricExtents);
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add(block.ExtensionDictionary);
                            }
                            else
                            {
                                //EntityLayer.Add("null");
                                //EntityType.Add("null");
                                //EntityHandle.Add("null");
                                //LineType.Add("null");
                                //EntityColor.Add("null");
                                //Coordinate.Add("null");
                                //Text.Add("null");
                                //Height.Add("null");
                                //LineWidth.Add("null");
                                //CRadius.Add("null");
                                //ExtenedEntity.Add("null");
                            }
                            #endregion 
                        }
                    }
                }
                trans.Commit();

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

        public static void delFile()
        {
            File.Delete("E:\\doc-fangfenglin\\" + MyPlugin.now + ".zip");
            delDir(MyPlugin.listPath);
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
}
