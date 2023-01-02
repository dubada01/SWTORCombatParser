using SWTORCombatParser.Model.CloudRaiding;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Utilities;
using Image = Google.Cloud.Vision.V1.Image;
using System;

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
        private static string ocr_url = "/ocr";
        private static string ocr_port = "8651";
        public static async Task<List<PlacedName>> GetCurrentPlayerLayoutLOCAL(Point topLeftOfFrame, Bitmap swtorRaidFrame,
            int numberOfRows, int numberOfColumns)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        using (var stream = new MemoryStream())
                        {
                            swtorRaidFrame.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            var test = new ByteArrayContent(stream.ToArray());
                            content.Add(test, "file", "orbs_overlay.png");
                            var baseUrl = DatabaseIPGetter.GetCurrentRemoteServerIP();
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

                                return GetNamesFromResponse(JsonConvert.DeserializeObject<Dictionary<string, List<List<double>>>>(jsonObject["response"].ToString()), swtorRaidFrame, topLeftOfFrame, numberOfRows, numberOfColumns);
                            }
                        }

                    }
                }
            }
            catch(Exception e)
            {
                Logging.LogError("Failed to pull names for raid frame overlay: " + e.Message);
                return new List<PlacedName>();
            }

        }

        private static List<PlacedName> GetNamesFromResponse(Dictionary<string, List<List<double>>> ocrResponse,Bitmap swtorRaidFrame,Point topLeftOfFrame,int numberOfRows, int numberOfColumns)
        {
            var validEntries = ocrResponse.Where(kvp=>kvp.Key.All(c=>char.IsLetter(c)|| c == '-' || c =='\'') && kvp.Key.Length > 2).OrderBy(kv=>kv.Value[0][0]);
            
            double rowHeights = swtorRaidFrame.Height / numberOfRows;
            double columnWidth = swtorRaidFrame.Width / numberOfColumns;
            
            var placedNames = new List<PlacedName>();

            foreach (var validEntry in validEntries)
            {
                var topLeft = validEntry.Value[0];
                var row = (int)(topLeft[1] / rowHeights);
                var column = (int)(topLeft[0] / columnWidth);
                var nameAtLocation = placedNames.FirstOrDefault(n => n.Row == row && n.Column == column);
                if (nameAtLocation == null)
                {
                    placedNames.Add(new PlacedName() { Name = validEntry.Key, Row = row, Column = column, Vertices = validEntry.Value.Select(v=>new Point((int)v[0]+ topLeftOfFrame.X,(int)v[1]+ topLeftOfFrame.Y)).ToList() });
                }
                else
                {
                    if(nameAtLocation.Vertices[0].Y < ((topLeft[1] +  topLeftOfFrame.Y) - 5))
                        continue;
                    nameAtLocation.Name += " " + validEntry.Key;
                }
            }
            return placedNames;
        }
        public static List<PlacedName> GetCurrentPlayerLayout(Point topLeftOfFrame, Bitmap swtorRaidFrame, int numberOfRows, int numberOfColumns)
        {
            var client = GoogleCloudPlatform.GetClient();

            var image = Image.FromBytes(ImageToByte2(swtorRaidFrame));
            var response = client.DetectText(image);

            var validEntites = response.Where(r =>r.Description.All(c=>char.IsLetter(c)|| c == '-' || c =='\'')).GroupBy(n=>n.Description).Select(g=>g.First()).ToList();

            double rowHeights = swtorRaidFrame.Height / numberOfRows;
            double columnWidth = swtorRaidFrame.Width / numberOfColumns;

            var placedNames = new List<PlacedName>();

            foreach (var validEntry in validEntites)
            {
                var topLeft = validEntry.BoundingPoly.Vertices[0];
                var row = (int)(topLeft.Y / rowHeights);
                var column = (int)(topLeft.X / columnWidth);
                var nameAtLocation = placedNames.FirstOrDefault(n => n.Row == row && n.Column == column);
                if (nameAtLocation == null)
                {
                    placedNames.Add(new PlacedName() { Name = validEntry.Description, Row = row, Column = column, Vertices = validEntry.BoundingPoly.Vertices.Select(v=>new Point(v.X+ topLeftOfFrame.X,v.Y+ topLeftOfFrame.Y)).ToList() });
                }
                else
                {
                    nameAtLocation.Name += " " + validEntry.Description;
                }
            }
            return placedNames;
        }
        private static byte[] ImageToByte2(Bitmap img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}
