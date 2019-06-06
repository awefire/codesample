using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Metadata;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;
using Xbim.Presentation;

namespace BimJson
{
    class Program
    {
        public static string IfcFileName { get; set; }//不带后缀名的ifc文件名
        public static string filePath { get; set; }//ifc文件的完整路径
        public static string filefloder { get; set; }//存放ifc的文件夹路径(最后带\)
        public static string xml_p { get; set; }
        public static string xml_t { get; set; }
        public static string now = "" + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second;

        static Dictionary<string, XbimPoint3D> namePosition = new Dictionary<string, XbimPoint3D>();
        static void Main(string[] args)
        {
            //Console.WriteLine("->请输入ifc文件名后回车:");
            filePath = args[0];
            IfcFileName = Path.GetFileNameWithoutExtension(filePath);
            filefloder = filePath.Replace(IfcFileName+".ifc","");
            xml_p = filePath.Remove(filePath.IndexOf("."),4)+"_P.xml";
            xml_t = filePath.Remove(filePath.IndexOf("."), 4) + "_T.xml";
            if (!File.Exists(filePath))
            {
                Console.WriteLine("文件不存在!");
                return;
            }
            Utils.exportXML();
            XmlToJson();
            //Utils.writeBATFile(IfcFileName);
            //Utils.RunBat(IfcFileName,Environment.CurrentDirectory+@"\");

            //File.Delete(Environment.CurrentDirectory+@"\"+IfcFileName+".bat");
            namePosition.Add(IfcFileName, new XbimPoint3D(0, 0, 0));

            GetIFCGeometry();
            //WriteXML.GetXml();

        }

        public static void GetIFCGeometry()
        {
            Console.WriteLine("->正在解析几何数据...");

            using (var model = IfcStore.Open(filePath))
            {
                var context = new Xbim3DModelContext(model);
                context.CreateContext();
                XbimGeometryData data;
                //获取不需要渲染的类型
                var excludedTypes = DefaultExclusions(model, ExcludedTypes);
                using (var geomStore = model.GeometryStore)
                {
                    using (var geomReader = geomStore.BeginRead())
                    {
                        var materialsByStyleId = new Dictionary<int, WpfMaterial>();
                        //get a list of all the unique style ids
                        var sstyleIds = geomReader.StyleIds;
                        foreach (var styleId in sstyleIds)
                        {
                            var wpfMaterial = GetWpfMaterial(model, styleId);
                            materialsByStyleId.Add(styleId, wpfMaterial);
                        }
                        //排除不需要渲染的类型
                        var shapeInstances = GetShapeInstancesToRender(geomReader, excludedTypes);
                        foreach (var shapeInstance in shapeInstances)
                        {
                            // work out style
                            var styleId = shapeInstance.StyleLabel > 0
                                ? shapeInstance.StyleLabel
                                : shapeInstance.IfcTypeId * -1;
                            if (!materialsByStyleId.ContainsKey(styleId))
                            {
                                // if the style is not available we build one by ExpressType
                                var material2 = GetWpfMaterialByType(model, shapeInstance.IfcTypeId);
                                materialsByStyleId.Add(styleId, material2);
                            }
                        }
                        if (shapeInstances.ToList().Count > 0)
                        {
                            //根据构建实例获取点、面、材质数据
                            GetIFCPointFaceMaterial(model, shapeInstances, geomReader, materialsByStyleId);
                        }
                    }
                }
            }
            Console.WriteLine("几何数据解析完成!");
        }

        /// <summary>
        /// 将ifc转化成ifcxml
        /// </summary>
        public static void GetIfcXML()
        {
            using (var model = IfcStore.Open(filePath))
            {
                model.SaveAs(IfcFileName + ".ifcxml");
            }
        }

        /// <summary>
        /// 获取几何数据
        /// </summary>
        /// <param name="model"></param>
        /// <param name="shapeInstances"></param>
        /// <param name="geomReader"></param>
        /// <param name="materialsByStyleId"></param>
        public static void GetIFCPointFaceMaterial(IModel model, IEnumerable<XbimShapeInstance> shapeInstances, IGeometryStoreReader geomReader, Dictionary<int, WpfMaterial> materialsByStyleId)
        {
            if (shapeInstances != null)
            {
                int index = 0;
                //string jsonString = "";
                StringBuilder jsonString = new StringBuilder();

                foreach (var shapeInstance in shapeInstances)
                {
                    //string faces_json = "";
                    StringBuilder faces_json = new StringBuilder();
                    string point_json = "";
                    
                    //globalId
                    var productId = shapeInstance.IfcProductLabel;
                    IIfcProduct product = model.Instances[productId] as IIfcProduct;
                    var globalId = product.GlobalId;
                    

                    //获取该实例的材质信息
                    var styleId = shapeInstance.StyleLabel > 0
                        ? shapeInstance.StyleLabel
                        : shapeInstance.IfcTypeId * -1;

                    var materialsByStyle = materialsByStyleId[styleId];
                    //var isTransparent = materialsByStyle.IsTransparent.ToString().ToLower();//是否透明
                    var materialsItem = materialsByStyle.Description.Split(' ');//材质描述

                    string materialString = "";
                    for (int i = 0; i < materialsItem.Length; i++)
                        if (i == 1 || i == 2 || i == 3 || i == 4)
                            materialString += ",\"" + materialsItem[i].Replace(":", "\":") + "";

                    if (materialString.Length > 0)
                        materialString = materialString.Substring(1).ToString();

                    //求单个模型4向矩阵信息
                    var transfor = shapeInstance.Transformation;
                    string matrixA = transfor.M11.ToString() + "," + transfor.M12.ToString() + "," + transfor.M13.ToString() + "," + transfor.M14.ToString() + ",";
                    string matrixB = transfor.M21.ToString() + "," + transfor.M22.ToString() + "," + transfor.M23.ToString() + "," + transfor.M24.ToString() + ",";
                    string matrixC = transfor.M31.ToString() + "," + transfor.M32.ToString() + "," + transfor.M33.ToString() + "," + transfor.M34.ToString() + ",";
                    string matrixXYZ = transfor.OffsetX.ToString() + "," + transfor.OffsetY.ToString() + "," + transfor.OffsetZ.ToString() + ",";
                    string matrixD = transfor.M44.ToString();

                    //拼接4向矩阵数据
                    string matrixString = matrixA + matrixB + matrixC + matrixXYZ + matrixD;
                    var matrixItem = matrixString.Split(',');//字符串数组

                    //添加每个模型的position数据
                    double[] position = new double[3];
                    

                    //转换成Int数组
                    double[] intItem = new double[16];
                    for (int i = 0; i < matrixItem.Length; i++)
                    {
                        intItem[i] = Convert.ToDouble(matrixItem[i]);               
                    }
                    position[0] = intItem[12];
                    position[1] = intItem[13];
                    position[2] = intItem[14];
                    string matrix4 = JsonConvert.SerializeObject(intItem);
                    string positionString = JsonConvert.SerializeObject(position);

                    IXbimShapeGeometryData geometrya = geomReader.ShapeGeometry(shapeInstance.ShapeGeometryLabel);
                    var data = ((IXbimShapeGeometryData)geometrya).ShapeData;

                    using (var stream = new MemoryStream(data))
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            var mesh = reader.ReadShapeTriangulation();

                            //查找模型所有的面
                            List<XbimFaceTriangulation> Faces = mesh.Faces as List<XbimFaceTriangulation>;
                            if (Faces.Count > 0)
                            {
                                foreach (var item in Faces)
                                {
                                    faces_json.Append(",{");
                                    faces_json.Append("\"IsPlanar\":" + item.IsPlanar.ToString().ToLower());
                                    faces_json.Append(",\"TriangleCount\":" + item.TriangleCount);
                                    faces_json.Append(",\"NormalCount\":" + 1);
                                    //faces_json.Append(",\"NormalCount\":" + item.NormalCount);
                                    faces_json.Append(",\"Indices\":" + JsonConvert.SerializeObject(item.Indices));
                                    faces_json.Append(",\"Normals\":[");
                                    var normals = item.Normals;
                                    //normal由于重复暂时只取一个，能极大减小数据量
                                    faces_json.Append("{\"X\":" + normals[0].Normal.X + ",\"Y\":" + normals[0].Normal.Y + ",\"Z\":" + normals[0].Normal.Z
                                            + ",\"Length\":" + normals[0].Normal.Length + ",\"Modulus\":" + normals[0].Normal.Modulus
                                            + ",\"U\":" + normals[0].U + ",\"V\":" + normals[0].V + "}");

                                    faces_json.Append("]}");
                                }
                            }
                            //faces_json = JsonConvert.SerializeObject(Faces);

                            //查找模型所有的点坐标
                            List<XbimPoint3D> Point = mesh.Vertices as List<XbimPoint3D>;
                            point_json = JsonConvert.SerializeObject(Point);

                            //将模型数据转换成json格式，保存至xxx.json文件
                            if (index != 0)
                                jsonString.Append(",");
                            jsonString.Append("{\"ObjectID\":\"" + globalId + "\",\"Position\":"+ positionString +",\"Matrix4\":" + matrix4 + ",\"Faces\":[" + faces_json.ToString().Substring(1) + "],\"Points\":" + point_json + ",\"Material\":{" + materialString + "}}");
                            //jsonString += ",{\"ObjectID\":\"" + globalId + "\",\"Matrix4\":" + matrix4 + ",\"Faces\":[" + faces_json.Substring(1) + "],\"Points\":" + point_json + ",\"Material\":{\"IsTransparent\":" + isTransparent + "," + materialString + "}}";
                            index++;
                        }
                    }
                }

                if (jsonString.Length > 0)
                {
                    string jsonObject = "{\"ID\":\"" + GuidTo16String() + "\",\"Name\":\"" + IfcFileName + "\",\"Position\":" + JsonConvert.SerializeObject(namePosition[IfcFileName]) + ",\"Geometry\":[";
                    jsonObject += jsonString.ToString();
                    //jsonObject += jsonString.ToString().Substring(1).ToString();
                    jsonObject += "]}";

                    if (jsonObject.Length > 2)
                    {
                        string fileNewName = filePath.Replace(".ifc","")+"_M.json";
                        byte[] myByte = System.Text.Encoding.UTF8.GetBytes(jsonObject);
                        using (FileStream fs = new FileStream(fileNewName, FileMode.Create))
                        {
                            fs.Write(myByte, 0, myByte.Length);
                        }
                    }
                }
            }
        }

        protected static IEnumerable<XbimShapeInstance> GetShapeInstancesToRender(IGeometryStoreReader geomReader, HashSet<short> excludedTypes)
        {
            var shapeInstances = geomReader.ShapeInstances
                .Where(s => s.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded
                            &&
                            !excludedTypes.Contains(s.IfcTypeId));
            return shapeInstances;
        }

        public static HashSet<short> DefaultExclusions(IModel model, List<Type> exclude)
        {
            var excludedTypes = new HashSet<short>();
            if (exclude == null)
                exclude = new List<Type>()
                {
                    typeof(IIfcSpace),
                    typeof(IIfcFeatureElement)
                };
            foreach (var excludedT in exclude)
            {
                ExpressType ifcT;
                if (excludedT.IsInterface && excludedT.Name.StartsWith("IIfc"))
                {
                    var concreteTypename = excludedT.Name.Substring(1).ToUpper();
                    ifcT = model.Metadata.ExpressType(concreteTypename);
                }
                else
                    ifcT = model.Metadata.ExpressType(excludedT);
                if (ifcT == null) // it could be a type that does not belong in the model schema
                    continue;
                foreach (var exIfcType in ifcT.NonAbstractSubTypes)
                {
                    excludedTypes.Add(exIfcType.TypeId);
                }
            }
            return excludedTypes;
        }

        protected static WpfMaterial GetWpfMaterial(IModel model, int styleId)
        {
            var sStyle = model.Instances[styleId] as IIfcSurfaceStyle;
            var texture = XbimTexture.Create(sStyle);
            texture.DefinedObjectId = styleId;
            var wpfMaterial = new WpfMaterial();
            wpfMaterial.CreateMaterial(texture);
            return wpfMaterial;
        }

        public static WpfMaterial GetWpfMaterialByType(IModel model, short typeid)
        {
            XbimColourMap _colourMap = new XbimColourMap();
            var prodType = model.Metadata.ExpressType(typeid);
            var v = _colourMap[prodType.Name];
            var texture = XbimTexture.Create(v);
            var material2 = new WpfMaterial();
            material2.CreateMaterial(texture);
            return material2;
        }

        /// <summary>
        /// The list of types that the engine will not consider in the generation of the scene, the exclusion code needs to be correctly implemented in the 
        /// configued ILayerStyler for the exclusion to work.
        /// </summary>
        public static List<Type> ExcludedTypes = new List<Type>()
        {
            typeof(Xbim.Ifc2x3.ProductExtension.IfcSpace),
            typeof(Xbim.Ifc4.ProductExtension.IfcSpace),
            typeof(Xbim.Ifc2x3.ProductExtension.IfcFeatureElement),
            typeof(Xbim.Ifc4.ProductExtension.IfcFeatureElement)
        };
        public static string GuidTo16String()
        {
            long i = 1;
            foreach (byte b in Guid.NewGuid().ToByteArray())
                i *= ((int)b + 1);
            return string.Format("{0:x}", i - DateTime.Now.Ticks);
        }

        /// <summary>
        /// XML转化成JSON
        /// </summary>
        public static void XmlToJson()
        {
           
            string outputjson_p = xml_p.Replace("_P.xml","_P.json");
            string outputjson_t = xml_t.Replace("_T.xml", "_T.json");
            /*step1转化属性数据*/
            Console.WriteLine("->正在转化属性数据...");
            try
            {
                Utils.ConvertXML(xml_p, outputjson_p);
            }
            catch
            {
                Console.WriteLine("->出错了");
                return;
            }
            Console.WriteLine("->属性数据{0}转化完成!",outputjson_p);
            /*step2转化结构树数据*/
            Console.WriteLine("->正在转化结构树数据...");
            try
            {
                Utils.ConvertXML(xml_t,outputjson_t);
            }
            catch
            {
                Console.WriteLine("->出错了");
                return;
            }
            Console.WriteLine("->结构树数据{0}转化完成!",outputjson_t);
        }

    }
}
