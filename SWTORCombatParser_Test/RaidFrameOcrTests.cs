using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Newtonsoft.Json;
using NUnit.Framework;
using SkiaSharp;
using SWTORCombatParser.Model.Overlays;

namespace SWTORCombatParser_Test;

public class RaidFrameOcrTests
{
    [TestCase(401, 805, 1, 4, 2)]
    [TestCase(401, 805, 2, 4, 2)]
    [TestCase(599, 503, 1, 6, 4)]
    [TestCase(599, 503, 2, 6, 4)]
    [TestCase(600, 240, 1, 4, 4)]
    public void SyntheticCapturePreservesResolutionAndMapsEveryCellAcrossDisplayScales(
        int frameWidth, int frameHeight, int captureScale, int rows, int columns)
    {
        using var source = new SKBitmap(frameWidth * captureScale, frameHeight * captureScale);
        source.Erase(SKColors.DarkSlateGray);
        using var stream = RaidFrameScreenGrab.EncodeRaidFramePng(source);
        using var encoded = SKBitmap.Decode(stream);
        Assert.That(encoded.Width, Is.EqualTo(source.Width));
        Assert.That(encoded.Height, Is.EqualTo(source.Height));

        var response = new Dictionary<string, List<List<double>>>();
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var name = $"Player{(char)('a' + row)}{(char)('a' + column)}";
                var left = (column + 0.1) * encoded.Width / columns;
                var top = (row + 0.25) * encoded.Height / rows;
                response[name] = Box(left, top, encoded.Width / (columns * 2d), encoded.Height / (rows * 5d));
            }

        var origin = new Point(-1920, 117);
        var results = AutoHOTOverlayPosition.GetNamesFromResponse(response, origin, frameWidth, frameHeight,
            rows, columns, encoded.Width, encoded.Height);
        Assert.That(results.Count, Is.EqualTo(rows * columns));
        foreach (var result in results)
        {
            Assert.That(result.Name, Is.EqualTo($"Player{(char)('a' + result.Row)}{(char)('a' + result.Column)}"));
            Assert.That(result.Vertices[0].X, Is.EqualTo(origin.X + (result.Column + 0.1) * frameWidth / columns).Within(0.0001));
            Assert.That(result.Vertices[0].Y, Is.EqualTo(origin.Y + (result.Row + 0.25) * frameHeight / rows).Within(0.0001));
        }
    }

    [Test]
    public void DelayedResponseRetainsItsOwnScaleAfterAnotherCapture()
    {
        var firstResponse = new Dictionary<string, List<List<double>>> { ["First"] = Box(160, 180, 30, 10) };
        using var otherImage = new SKBitmap(200, 200);
        using var secondCapture = RaidFrameScreenGrab.EncodeRaidFramePng(otherImage);

        var first = AutoHOTOverlayPosition.GetNamesFromResponse(firstResponse, new Point(10, 20),
            800, 1200, 4, 2, 200, 300).Single();
        Assert.That(first.Row, Is.EqualTo(2));
        Assert.That(first.Column, Is.EqualTo(1));
        Assert.That(first.Vertices[0], Is.EqualTo(new Point(650, 740)));
    }

    [Test]
    public void LosslessEncodingPreservesSmallLetterStrokesInTallRaidFrames()
    {
        using var source = new SKBitmap(400, 800);
        source.Erase(new SKColor(12, 20, 38));
        // A five-pixel-wide letter with one-pixel strokes is a deterministic stand-in for
        // small name text. Preserve every stroke even at the bottom of a tall raid frame.
        var glyph = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
        foreach (var top in new[] { 20, 120, 220, 320, 420, 520, 620, 720 })
            for (var row = 0; row < glyph.Length; row++)
                for (var column = 0; column < glyph[row].Length; column++)
                    if (glyph[row][column] == '1') source.SetPixel(12 + column, top + row, SKColors.White);

        using var stream = RaidFrameScreenGrab.EncodeRaidFramePng(source);
        Assert.That(stream.Position, Is.Zero);
        using var decoded = SKBitmap.Decode(stream);
        Assert.That(decoded.Width, Is.EqualTo(source.Width));
        Assert.That(decoded.Height, Is.EqualTo(source.Height));
        Assert.That(decoded.Pixels, Is.EqualTo(source.Pixels));
    }

    [Test]
    public void MalformedAndOutOfBoundsBoxesDoNotDiscardGoodPlayers()
    {
        var response = new Dictionary<string, List<List<double>>>
        {
            ["Player"] = Box(110, 120, 35, 10),
            ["Empty"] = new(),
            ["Missing"] = null,
            ["Partial"] = new() { new() { 1 } },
            ["Invalid"] = Box(double.NaN, 20, 20, 10),
            ["Negative"] = Box(-1, 120, 30, 10),
            ["Outside"] = Box(210, 120, 30, 10),
            ["Overflow"] = Box(190, 120, 30, 10),
            ["100%"] = Box(10, 20, 30, 10)
        };
        var result = AutoHOTOverlayPosition.GetNamesFromResponse(response, default, 400, 600, 4, 2, 200, 300);
        Assert.That(result.Select(n => n.Name), Is.EqualTo(new[] { "Player" }));
    }

    [Test]
    public void NameLineWinsOverStatusTextFurtherLeftAndJoinsSplitWords()
    {
        var response = new Dictionary<string, List<List<double>>>
        {
            ["Dead"] = Box(1, 40, 20, 8),
            ["Walker"] = Box(42, 20, 30, 8),
            ["Sky"] = Box(20, 21, 20, 8)
        };
        var result = AutoHOTOverlayPosition.GetNamesFromResponse(response, default, 400, 600, 4, 2, 200, 300).Single();
        Assert.That(result.Name, Is.EqualTo("Sky Walker"));
        Assert.That(result.Vertices[0], Is.EqualTo(new Point(40, 40)));
        Assert.That(result.Vertices[2], Is.EqualTo(new Point(144, 58)));
    }

    [TestCase("Sky Walker")]
    [TestCase("O'Rian")]
    [TestCase("O\u2019Rian")]
    [TestCase("Shae-Vizla")]
    [TestCase("\u00c9lana")]
    [TestCase("E\u0301lana")]
    [TestCase("Li")]
    public void CharacterNamesPreserveSpacesAccentsAndPunctuation(string name)
    {
        var result = AutoHOTOverlayPosition.GetNamesFromResponse(
            new() { [name] = Box(20, 20, 50, 10) }, default, 400, 600, 4, 2, 200, 300);
        Assert.That(result.Single().Name, Is.EqualTo(name));
    }

    [TestCase("null")]
    [TestCase("<html>server offline</html>")]
    [TestCase("{}")]
    [TestCase("{\"status\":\"error\",\"response\":{}}")]
    [TestCase("{\"status\":\"ok\",\"response\":null}")]
    public void ServiceErrorsAreDistinctFromAnEmptySuccessfulScan(string json)
    {
        var result = Parse(json);
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Empty);
        Assert.That(result.Names, Is.Empty);
    }

    [Test]
    public void EmptySuccessfulScanHasNoServiceError()
    {
        var result = Parse("{\"status\":\"ok\",\"response\":{}}");
        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorMessage, Is.Empty);
        Assert.That(result.Names, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ServiceResponseSupportsObjectOrEncodedJsonAndIgnoresMalformedEntries(bool encodedJson)
    {
        var boxes = new Dictionary<string, object>
        {
            ["Player"] = Box(20, 20, 50, 10),
            ["Broken"] = "not a bounding box",
            ["BadNumber"] = new[] { new[] { "not a number", "20" } }
        };
        var json = JsonConvert.SerializeObject(new { status = "ok", response = encodedJson ? (object)JsonConvert.SerializeObject(boxes) : boxes });
        var result = Parse(json);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Names.Select(n => n.Name), Is.EqualTo(new[] { "Player" }));
    }

    [Test]
    public void MaskingDoesNotDriftWhenHeightIsNotDivisibleByRows()
    {
        using var bitmap = new SKBitmap(5, 403);
        bitmap.Erase(SKColors.White);
        RaidFrameScreenGrab.RemoveOverlayNames(bitmap, 8);
        Assert.That(bitmap.GetPixel(0, 352).Alpha, Is.Zero);
        Assert.That(bitmap.GetPixel(0, 368).Alpha, Is.Zero);
        Assert.That(bitmap.GetPixel(0, 369), Is.EqualTo(SKColors.White));
    }

    [Test]
    public void MaskingUsesOverlayMonitorScale()
    {
        using var bitmap = new SKBitmap(5, 400);
        bitmap.Erase(SKColors.White);
        RaidFrameScreenGrab.RemoveOverlayNames(bitmap, 4, 2d);
        Assert.That(bitmap.GetPixel(0, 132).Alpha, Is.Zero);
        Assert.That(bitmap.GetPixel(0, 133), Is.EqualTo(SKColors.White));
    }

    private static RaidFrameDetectionResult Parse(string json) =>
        AutoHOTOverlayPosition.ParseResponse(json, default, 400, 600, 4, 2, 200, 300);

    private static List<List<double>> Box(double left, double top, double width, double height) =>
        new() { new() { left, top }, new() { left + width, top }, new() { left + width, top + height }, new() { left, top + height } };
}
