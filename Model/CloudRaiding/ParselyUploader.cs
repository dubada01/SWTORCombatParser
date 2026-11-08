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
        private static readonly string parselyURL = "https://parsely.io/api/upload2";
        private static readonly HttpClient client = CreateClient();
        internal static event Action<bool, string> UploadCompleted = delegate { };
        internal static event Action UploadStarted = delegate { };  
        internal static async Task<string> UploadCurrentCombat(string currentlySelectedLogName)
        {
            if (Model.LogParsing.LogReplayContext.IsReplayFile(currentlySelectedLogName))
                return "Debug replays cannot be uploaded. Open the original combat log to upload it.";
            if (string.IsNullOrEmpty(currentlySelectedLogName) || !File.Exists(currentlySelectedLogName))
            {
                UploadCompleted(false, "");
                return "Error: No log file selected";
            }
            try
            {
                var logText = ReadAllText(currentlySelectedLogName);
                var response = await TryUploadText(logText, currentlySelectedLogName);
                return response.WasSuccess ? "" : $"Error: Failed to upload log\r\n {response.ErrorMessage}";
            }
            catch (Exception ex)
            {
                UploadCompleted(false, "");
                return $"Error: Failed to read log\r\n {ex.Message}";
            }
        }
        public static async Task<LogUploadResponse> TryUploadText(string logText, string logFileName)
        {
            if (Model.LogParsing.LogReplayContext.IsReplayFile(logFileName))
                return new LogUploadResponse { WasSuccess = false, ErrorMessage = "Debug replays cannot be uploaded. Open the original combat log to upload it." };
            UploadStarted();
            try
            {
                var zippedData = Zip(logText);
                using (var content = new MultipartFormDataContent())
                {
                    var test = new ByteArrayContent(zippedData);
                    test.Headers.Add("Content-Type", "text/html");
                    test.Headers.Add("Content-Transfer-Encoding", "binary");

                    content.Add(test, "file", Path.GetFileName(logFileName));
                    if (Settings.HasSetting("username"))
                    {
                        content.Add(new StringContent(Settings.ReadSettingOfType<string>("username").Trim('"')), "username");
                        content.Add(new StringContent(Crypto.DecryptStringAES(Settings.ReadSettingOfType<string>("password").Trim('"'), "parselyInfo")), "password");
                        if (!string.IsNullOrEmpty(Settings.ReadSettingOfType<string>("guild").Trim('"')))
                            content.Add(new StringContent(Settings.ReadSettingOfType<string>("guild").Trim('"')), "guild");
                    }
                    content.Add(new StringContent("1"), "public");
                    using (var message = await client.PostAsync(parselyURL, content))
                    {
                        var response = await message.Content.ReadAsStringAsync();
                        if (!message.IsSuccessStatusCode || response.Contains("NOT OK", StringComparison.OrdinalIgnoreCase) || response.Contains("error", StringComparison.OrdinalIgnoreCase))
                        {
                            UploadCompleted(false, "");
                            return new LogUploadResponse { WasSuccess = false, ErrorMessage = $"HTTP {(int)message.StatusCode}: {response}" };
                        }
                        XmlDocument xdoc = new XmlDocument { XmlResolver = null };
                        using var xmlReader = XmlReader.Create(new StringReader(response), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                        xdoc.Load(xmlReader);
                        var parselyLink = xdoc.GetElementsByTagName("file")[0]?.InnerText;
                        if (!Uri.TryCreate(parselyLink, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps)
                            throw new InvalidDataException("Parsely returned an invalid upload link.");
                        UploadCompleted(true, parselyLink);
                        return new LogUploadResponse { WasSuccess = true, ParselyLink = parselyLink };
                    }
                }
            }
            catch (Exception ex)
            {
                UploadCompleted(false, "");
                return new LogUploadResponse { WasSuccess = false, ErrorMessage = ex.Message };
            }
        }
        private static HttpClient CreateClient()
        {
            var connection = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
            connection.DefaultRequestHeaders.Add("User-Agent", "Orbs v" + Assembly.GetExecutingAssembly().GetName().Version);
            return connection;
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
