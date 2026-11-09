using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace SWTORCombatParser.Model.Overlays
{
    public class PlacedName
    {
        public string Name { get; set; } = "";
        public int Row { get; set; }
        public int Column { get; set; }
        public List<Point> Vertices { get; set; } = new();
        public Bitmap? PixelsAtNameLocation { get; set; }
    }

    public sealed class RaidFrameDetectionResult
    {
        public bool Success { get; init; }
        public List<PlacedName> Names { get; init; } = new();
        public string ErrorMessage { get; init; } = "";
    }

    public static class AutoHOTOverlayPosition
    {
        private const string OcrUrl = "http://orbs-stats.com:27506/ocr";
        private const string AwsOcrUrl = "http://ec2-35-85-227-238.us-west-2.compute.amazonaws.com:27506/ocr";
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

        public static Task<RaidFrameDetectionResult> DetectCurrentPlayerLayoutAsync(Point topLeftOfFrame,
            MemoryStream raidFrameStream, int numberOfRows, int numberOfColumns, int height, int width,
            CancellationToken cancellationToken = default)
        {
            return DetectAsync(OcrUrl, topLeftOfFrame, raidFrameStream, numberOfRows, numberOfColumns,
                height, width, cancellationToken);
        }

        public static async Task<List<PlacedName>> GetCurrentPlayerLayoutLOCAL(Point topLeftOfFrame,
            MemoryStream raidFrameStream, int numberOfRows, int numberOfColumns, int height, int width,
            CancellationToken cancellationToken = default)
        {
            return (await DetectCurrentPlayerLayoutAsync(topLeftOfFrame, raidFrameStream, numberOfRows,
                numberOfColumns, height, width, cancellationToken)).Names;
        }

        public static async Task<List<PlacedName>> GetCurrentPlayerLayoutAWS(Point topLeftOfFrame,
            MemoryStream raidFrameStream, int numberOfRows, int numberOfColumns, int height, int width,
            CancellationToken cancellationToken = default)
        {
            return (await DetectAsync(AwsOcrUrl, topLeftOfFrame, raidFrameStream, numberOfRows,
                numberOfColumns, height, width, cancellationToken)).Names;
        }

        private static async Task<RaidFrameDetectionResult> DetectAsync(string url, Point topLeftOfFrame,
            MemoryStream raidFrameStream, int rows, int columns, int height, int width,
            CancellationToken cancellationToken)
        {
            if (height <= 0 || width <= 0 || rows <= 0 || columns <= 0)
                return Failure("Set a valid raid frame size, row count, and column count before detecting names.");

            try
            {
                // Keep the image dimensions with this request. A later capture must not change how
                // coordinates in an earlier response are interpreted (including Retina captures).
                var bytes = raidFrameStream.ToArray();
                using var encodedImage = SKData.CreateCopy(bytes);
                using var codec = SKCodec.Create(encodedImage);
                if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
                    return Failure("The raid frame image could not be read. Reposition the frame and try again.");
                var imageWidth = codec.Info.Width;
                var imageHeight = codec.Info.Height;

                using var content = new MultipartFormDataContent();
                var imageContent = new ByteArrayContent(bytes);
                imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                content.Add(imageContent, "file", "orbs_overlay.png");
                using var message = await Client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
                if (!message.IsSuccessStatusCode)
                    return Failure("The name detection service is unavailable. Try again or use manual alignment.");

                var response = await message.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return ParseResponse(response, topLeftOfFrame, width, height, rows, columns, imageWidth, imageHeight);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure("Name detection timed out. Try again or use manual alignment.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                return Failure("Could not reach the name detection service. Check your connection or use manual alignment.");
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to detect raid frame names: " + ex.Message);
                return Failure("Name detection failed. Try again or use manual alignment.");
            }
        }

        public static RaidFrameDetectionResult ParseResponse(string response, Point frameOrigin,
            int frameWidth, int frameHeight, int rows, int columns, int imageWidth, int imageHeight)
        {
            try
            {
                var json = JObject.Parse(response);
                if (!string.Equals((string?)json["status"], "ok", StringComparison.OrdinalIgnoreCase) ||
                    json["response"] == null)
                    return Failure("The name detection service could not read the image. Try again or use manual alignment.");

                var boxes = json["response"] is JObject objectResponse
                    ? objectResponse
                    : JObject.Parse(json["response"]!.ToString());
                var entries = new Dictionary<string, List<List<double>>>();
                foreach (var entry in boxes.Properties())
                {
                    // One malformed OCR box should not discard every other detected player.
                    try
                    {
                        var vertices = entry.Value.ToObject<List<List<double>>>();
                        if (vertices != null)
                            entries[entry.Name] = vertices;
                    }
                    catch (JsonException) { }
                    catch (FormatException) { }
                }

                return new RaidFrameDetectionResult
                {
                    Success = true,
                    Names = GetNamesFromResponse(entries, frameOrigin, frameWidth, frameHeight,
                        rows, columns, imageWidth, imageHeight)
                };
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is FormatException)
            {
                return Failure("The name detection service returned an invalid response. Try again or use manual alignment.");
            }
        }

        public static List<PlacedName> GetNamesFromResponse(Dictionary<string, List<List<double>>> ocrResponse,
            Point frameOrigin, int frameWidth, int frameHeight, int rows, int columns, int imageWidth, int imageHeight)
        {
            if (frameWidth <= 0 || frameHeight <= 0 || rows <= 0 || columns <= 0 || imageWidth <= 0 || imageHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(frameWidth), "Frame and image dimensions and grid counts must be positive.");

            var candidates = new List<PlacedName>();
            foreach (var entry in ocrResponse)
            {
                var name = string.Join(" ", entry.Key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (name.Count(char.IsLetter) < 2 || !name.All(IsNameCharacter) || entry.Value == null ||
                    entry.Value.Count < 4 || entry.Value.Any(p => p == null || p.Count < 2 ||
                        !double.IsFinite(p[0]) || !double.IsFinite(p[1])))
                    continue;

                var left = entry.Value.Min(p => p[0]);
                var top = entry.Value.Min(p => p[1]);
                var right = entry.Value.Max(p => p[0]);
                var bottom = entry.Value.Max(p => p[1]);
                if (left < 0 || top < 0 || left >= imageWidth || top >= imageHeight ||
                    right > imageWidth || bottom > imageHeight || right <= left || bottom <= top)
                    continue;

                candidates.Add(new PlacedName
                {
                    Name = name,
                    Row = Math.Min(rows - 1, (int)(top * rows / imageHeight)),
                    Column = Math.Min(columns - 1, (int)(left * columns / imageWidth)),
                    Vertices = entry.Value.Select(p => new Point(frameOrigin.X + p[0] * frameWidth / imageWidth,
                        frameOrigin.Y + p[1] * frameHeight / imageHeight)).ToList()
                });
            }

            var names = new List<PlacedName>();
            foreach (var cell in candidates.GroupBy(n => (n.Row, n.Column)).OrderBy(g => g.Key.Row).ThenBy(g => g.Key.Column))
            {
                // Player names occupy the first text line in a frame. Sort vertically first so a
                // left-aligned health/status label below the name cannot win based on X position.
                var first = cell.OrderBy(n => n.Vertices.Min(p => p.Y)).First();
                var firstTop = first.Vertices.Min(p => p.Y);
                var firstBottom = first.Vertices.Max(p => p.Y);
                var line = cell.Where(n =>
                {
                    var top = n.Vertices.Min(p => p.Y);
                    var bottom = n.Vertices.Max(p => p.Y);
                    return Math.Min(bottom, firstBottom) - Math.Max(top, firstTop) >=
                           Math.Min(bottom - top, firstBottom - firstTop) / 2;
                }).OrderBy(n => n.Vertices.Min(p => p.X)).ToList();
                var left = line.Min(n => n.Vertices.Min(p => p.X));
                var top = line.Min(n => n.Vertices.Min(p => p.Y));
                var right = line.Max(n => n.Vertices.Max(p => p.X));
                var bottom = line.Max(n => n.Vertices.Max(p => p.Y));
                names.Add(new PlacedName
                {
                    Name = string.Join(" ", line.Select(n => n.Name)),
                    Row = first.Row,
                    Column = first.Column,
                    Vertices = new List<Point> { new(left, top), new(right, top), new(right, bottom), new(left, bottom) }
                });
            }
            return names;
        }

        private static bool IsNameCharacter(char c) => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'' ||
            c == '\u2019' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;

        private static RaidFrameDetectionResult Failure(string message) => new() { ErrorMessage = message };
    }
}
