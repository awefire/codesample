using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace BimJson
{
    public static class Converter
    {
        /// <summary>
        /// converts xml string to json string
        /// </summary>
        /// <param name="xml">xml data as string</param>
        /// <returns>json data as string</returns>
        public static string ConvertToJson(string xml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            //strip comments from xml
            var comments = doc.SelectNodes("//comment()");

            if (comments != null)
            {
                foreach (var node in comments.Cast<XmlNode>())
                {
                    if (node.ParentNode != null)
                        node.ParentNode.RemoveChild(node);
                }
            }

            var rawJsonText = JsonConvert.SerializeXmlNode(doc.DocumentElement, Newtonsoft.Json.Formatting.Indented);

            //strip the @ and # characters
            var strippedJsonText = Regex.Replace(rawJsonText, "(?<=\")(@)(?!.*\":\\s )", string.Empty, RegexOptions.IgnoreCase);
            strippedJsonText = Regex.Replace(strippedJsonText, "(?<=\")(#)(?!.*\":\\s )", string.Empty, RegexOptions.IgnoreCase);

            // unquote numbers and booleans
            strippedJsonText = Regex.Replace(strippedJsonText, "\\\"([\\d\\.]+)\\\"", "$1", RegexOptions.IgnoreCase);
            strippedJsonText = Regex.Replace(strippedJsonText, "\\\"(true|false)\\\"", "$1", RegexOptions.IgnoreCase);

            return strippedJsonText;
        }
    }
}
