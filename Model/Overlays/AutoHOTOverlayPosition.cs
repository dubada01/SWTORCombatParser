using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;

namespace SWTORCombatParser.Model.Overlays
{
    public class PlacedName
    {
        public string Name { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public List<Point> Vertices { get; set; }
        public Bitmap PixelsAtNameLocation { get; set; }
    }
    public static class AutoHOTOverlayPosition
    {
        private static string aws_url = "http://ec2-35-85-227-238.us-west-2.compute.amazonaws.com";
        private static string ocr_url = "/ocr";
        private static string ocr_port = "27506";
        public static async Task<List<PlacedName>> GetCurrentPlayerLayoutLOCAL(Point topLeftOfFrame, MemoryStream raidFrameStream,
            int numberOfRows, int numberOfColumns, int height, int width)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        using (raidFrameStream)
                        {
                            var test = new ByteArrayContent(raidFrameStream.ToArray());
                            content.Add(test, "file", "orbs_overlay.png");
                            var baseUrl = DatabaseIPGetter.GetCurrentRemoteServerIP();
                            baseUrl = "orbs-stats.com";
                            var fullUrl = $"http://{baseUrl}:{ocr_port}{ocr_url}";
                            using (var message = await client.PostAsync(fullUrl, content))
                            {
                                var textResponse = await message.Content.ReadAsStringAsync();
                                if (!message.IsSuccessStatusCode)
                                {
                                    Logging.LogError("Failed to pull names for raid frame overlay: " + textResponse);
                                    return new List<PlacedName>();
                                }
                                var jsonObject = JsonConvert.DeserializeObject<JObject>(textResponse);
                                if (jsonObject["status"].ToString() != "ok")
                                {
                                    Logging.LogError("Failed to pull names for raid frame overlay: " + textResponse);
                                    return new List<PlacedName>();
                                }

                                return GetNamesFromResponse(JsonConvert.DeserializeObject<Dictionary<string, List<List<double>>>>(jsonObject["response"].ToString()), height, width, topLeftOfFrame, numberOfRows, numberOfColumns);
                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to pull names for raid frame overlay: " + e.Message);
                return new List<PlacedName>();
            }

        }
        public static async Task<List<PlacedName>> GetCurrentPlayerLayoutAWS(Point topLeftOfFrame, MemoryStream raidFrameStream,
    int numberOfRows, int numberOfColumns, int height, int width)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        using (raidFrameStream)
                        {
                            var test = new ByteArrayContent(raidFrameStream.ToArray());
                            content.Add(test, "file", "orbs_overlay.png");
                            var fullUrl = $"{aws_url}:{ocr_port}{ocr_url}";
                            using (var message = await client.PostAsync(fullUrl, content))
                            {
                                var textResponse = await message.Content.ReadAsStringAsync();
                                if (!message.IsSuccessStatusCode)
                                {
                                    Logging.LogError("Failed to pull names for raid frame overlay: " + textResponse);
                                    return new List<PlacedName>();
                                }
                                var jsonObject = JsonConvert.DeserializeObject<JObject>(textResponse);
                                if (jsonObject["status"].ToString() != "ok")
                                {
                                    Logging.LogError("Failed to pull names for raid frame overlay: " + textResponse);
                                    return new List<PlacedName>();
                                }

                                return GetNamesFromResponse(JsonConvert.DeserializeObject<Dictionary<string, List<List<double>>>>(jsonObject["response"].ToString()), height, width, topLeftOfFrame, numberOfRows, numberOfColumns);
                            }
                        }

                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to pull names for raid frame overlay: " + e.Message);
                return new List<PlacedName>();
            }

        }
        private static List<PlacedName> GetNamesFromResponse(Dictionary<string, List<List<double>>> ocrResponse, int height, int width, Point topLeftOfFrame, int numberOfRows, int numberOfColumns)
        {
            var validEntries = ocrResponse.Where(kvp => kvp.Key.All(c => char.IsLetter(c) || c == '-' || c == '\'') && kvp.Key.Length > 2).OrderBy(kv => kv.Value[0][0]);
            var correctedEntries = validEntries.Select(ve =>
                new KeyValuePair<string, List<List<double>>>(ve.Key,
                    ve.Value.Select(coord => coord.Select(ConvertCoordWithCompressionFactor).ToList()).ToList()));
            double rowHeights = height / (double)numberOfRows;
            double columnWidth = width / (double)numberOfColumns;

            var placedNames = new List<PlacedName>();

            foreach (var validEntry in correctedEntries)
            {
                var topLeft = validEntry.Value[0];
                var row = Math.Min(numberOfRows - 1, (int)(topLeft[1] / rowHeights));
                var column = Math.Min(numberOfColumns - 1, (int)(topLeft[0] / columnWidth));
                var nameAtLocation = placedNames.FirstOrDefault(n => n.Row == row && n.Column == column);
                if (nameAtLocation == null)
                {
                    placedNames.Add(new PlacedName() { Name = validEntry.Key, Row = row, Column = column, Vertices = validEntry.Value.Select(v => new Point((int)v[0] + topLeftOfFrame.X, (int)v[1] + topLeftOfFrame.Y)).ToList() });
                }
                else
                {
                    if (nameAtLocation.Vertices[0].Y < ((topLeft[1] + topLeftOfFrame.Y) - 5))
                        continue;
                    nameAtLocation.Name += " " + validEntry.Key;
                }
            }
            return placedNames;
        }

        private static double ConvertCoordWithCompressionFactor(double value)
        {
            #if MACOS
            var scalingFactor = 1d;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {

                scalingFactor = desktop.MainWindow.RenderScaling;
                return value/scalingFactor;

            }
            #endif
            return value / RaidFrameScreenGrab.CurrentCompressionFactor;
        }
    }
}
