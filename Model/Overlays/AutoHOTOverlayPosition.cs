using SWTORCombatParser.Model.CloudRaiding;
using Google.Cloud.Vision.V1;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = Google.Cloud.Vision.V1.Image;

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
        public static List<PlacedName> GetCurrentPlayerLayout(Point topLeftOfFrame, Bitmap swtorRaidFrame, int numberOfRows, int numberOfColumns)
        {
            var client = GoogleCloudPlatform.GetClient();
            var image = Image.FromBytes(ImageToByte2(swtorRaidFrame));
            var response = client.DetectText(image);

            var validEntites = response.Where(r => !r.Description.Contains('\n') && !r.Description.Any(c => char.IsDigit(c)));

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
