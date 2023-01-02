using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Security.Policy;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using System.Xml;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class ParselyUploader
    {
        private static string parselyURL = "https://parsely.io/api/upload2";
        internal static event Action<bool,string> UploadCompleted = delegate { };
        internal static async Task UploadCurrentCombat(string currentlySelectedLogName)
        {
            if (string.IsNullOrEmpty(currentlySelectedLogName) || !File.Exists(currentlySelectedLogName))
            {
                UploadCompleted(false,"");
                return;
            }
            var zippedData = Zip(ReadAllText(currentlySelectedLogName));
            var parselyLink = "";
            using(var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Orbs v" + Assembly.GetExecutingAssembly().GetName().Version);
                using(var content = new MultipartFormDataContent())
                {
                    var test = new ByteArrayContent(zippedData);
                    test.Headers.Add("Content-Type", "text/html");
                    test.Headers.Add("Content-Transfer-Encoding", "binary");
                    
                    content.Add(test, "file", currentlySelectedLogName);
                    content.Add(new StringContent("1"),"public");
                    using (var message = await client.PostAsync(parselyURL, content))
                    {
                        var response = await message.Content.ReadAsStringAsync();
                        if (response.Contains("NOT OK"))
                        {
                            UploadCompleted(false,"");
                            return;
                        }
                        XmlDocument xdoc = new XmlDocument();
                        xdoc.LoadXml(response);
                        parselyLink = xdoc.GetElementsByTagName("file")[0].InnerText;
                    }
                }
            }
            UploadCompleted(true, parselyLink);
            return;
        }
        static string ReadAllText(string file)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream,Encoding.GetEncoding(1252)))
                return textReader.ReadToEnd();
        }
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(string str)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var bytes = Encoding.GetEncoding(1252).GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }
    }

}
