using SWTORCombatParser.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public class LogUploadResponse
    {
        public string ParselyLink { get; set; }
        public bool WasSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }
    public static class ParselyUploader
    {
        private static string parselyURL = "https://parsely.io/api/upload2";
        internal static event Action<bool, string> UploadCompleted = delegate { };
        internal static event Action UploadStarted = delegate { };  
        internal static async Task<string> UploadCurrentCombat(string currentlySelectedLogName)
        {
            if (string.IsNullOrEmpty(currentlySelectedLogName) || !File.Exists(currentlySelectedLogName))
            {
                UploadCompleted(false, "");
                return "Error: No log file selected";
            }
            var logText = ReadAllText(currentlySelectedLogName);
            var response = await TryUploadText(logText, currentlySelectedLogName);
            return response.WasSuccess ? "" : $"Error: Failed to upload log\r\n {response.ErrorMessage}";
        }
        public static async Task<LogUploadResponse> TryUploadText(string logText, string logFileName)
        {
            UploadStarted();
            var zippedData = Zip(logText);
            var parselyLink = "";
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(300);
                client.DefaultRequestHeaders.Add("User-Agent", "Orbs v" + Assembly.GetExecutingAssembly().GetName().Version);
                using (var content = new MultipartFormDataContent())
                {
                    var test = new ByteArrayContent(zippedData);
                    test.Headers.Add("Content-Type", "text/html");
                    test.Headers.Add("Content-Transfer-Encoding", "binary");

                    content.Add(test, "file", logFileName);
                    if (Settings.HasSetting("username"))
                    {
                        content.Add(new StringContent(Settings.ReadSettingOfType<string>("username").Trim('"')), "username");
                        content.Add(new StringContent(Crypto.DecryptStringAES(Settings.ReadSettingOfType<string>("password").Trim('"'), "parselyInfo")), "password");
                        if (!string.IsNullOrEmpty(Settings.ReadSettingOfType<string>("guild").Trim('"')))
                            content.Add(new StringContent(Settings.ReadSettingOfType<string>("guild").Trim('"')), "guild");
                    }
                    content.Add(new StringContent("1"), "public");
                    try
                    {
                        using (var message = await client.PostAsync(parselyURL, content))
                        {
                            var response = await message.Content.ReadAsStringAsync();
                            if (response.Contains("NOT OK") || response.Contains("error"))
                            {
                                UploadCompleted(false, "");
                                return new LogUploadResponse { WasSuccess = false, ErrorMessage = response};
                            }
                            XmlDocument xdoc = new XmlDocument();
                            xdoc.LoadXml(response);
                            parselyLink = xdoc.GetElementsByTagName("file")[0].InnerText;
                            UploadCompleted(true, parselyLink);
                            return new LogUploadResponse { WasSuccess = true, ParselyLink = parselyLink };
                        }
                    }
                    catch (Exception ex)
                    {
                        UploadCompleted(false, "");
                        return new LogUploadResponse { WasSuccess = false, ErrorMessage = ex.Message };
                    }
                }
            }
        }
        static string ReadAllText(string file)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream, Encoding.GetEncoding(1252)))
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
