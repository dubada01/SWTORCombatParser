using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class OfflineStartupTests
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private string _directory = null!;
    private object _oldDirectory = null!;
    private object _oldPath = null!;
    private string _oldReason = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "OrbsOfflineTests", Guid.NewGuid().ToString("N"));
        _oldDirectory = typeof(Settings).GetField("_appDataPath", Static)!.GetValue(null)!;
        _oldPath = typeof(Settings).GetField("_settingsPath", Static)!.GetValue(null)!;
        _oldReason = Settings.OfflineModeRequiredReason;
        typeof(Settings).GetField("_appDataPath", Static)!.SetValue(null, _directory);
        typeof(Settings).GetField("_settingsPath", Static)!.SetValue(null, Path.Combine(_directory, "settings.json"));
        Settings.OfflineModeRequiredReason = null;
    }

    [TearDown]
    public void TearDown()
    {
        typeof(Settings).GetField("_appDataPath", Static)!.SetValue(null, _oldDirectory);
        typeof(Settings).GetField("_settingsPath", Static)!.SetValue(null, _oldPath);
        Settings.OfflineModeRequiredReason = _oldReason;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public async Task OfflineBlocksRequestsBeforeTheNetworkHandlerAndAllowsThemAfterOptingIn()
    {
        var transport = new RecordingHandler();
        using var client = OfflineHttpHandler.CreateClient(transport);
        Settings.WriteSetting("offline_mode", true);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await client.GetAsync("https://example.invalid/"));
        Assert.That(transport.Requests, Is.Zero);
        Settings.WriteSetting("offline_mode", false);
        using var response = await client.GetAsync("https://example.invalid/");
        Assert.That(transport.Requests, Is.EqualTo(1));
    }

    [Test]
    public void MissingOpenSslCannotBeOverriddenByAnOnlineToggle()
    {
        Settings.WriteSetting("offline_mode", false);
        Settings.OfflineModeRequiredReason = "Missing OpenSSL";
        Assert.That(Settings.ReadSettingOfType<bool>("offline_mode"), Is.True);
        Settings.WriteSetting("offline_mode", false);
        Settings.OfflineModeRequiredReason = null; // Next launch after installing the dependency.
        Assert.That(Settings.ReadSettingOfType<bool>("offline_mode"), Is.True, "Offline choice remains saved.");
        Settings.WriteSetting("offline_mode", false);
        Assert.That(Settings.ReadSettingOfType<bool>("offline_mode"), Is.False);
    }

    [Test]
    public async Task OfflineUploadsAndOcrReturnUsefulErrorsBeforeReadingCredentialsOrImages()
    {
        Settings.WriteSetting("offline_mode", true);
        Settings.WriteSetting("username", "test");
        Settings.WriteSetting("password", "intentionally-invalid-encrypted-password");
        var upload = await ParselyUploader.TryUploadText("log", "combat.txt");
        using var unreadableImage = new MemoryStream();
        var detection = await AutoHOTOverlayPosition.DetectCurrentPlayerLayoutAsync(new Point(), unreadableImage, 4, 2, 400, 200);
        Assert.Multiple(() =>
        {
            Assert.That(upload.WasSuccess, Is.False);
            Assert.That(upload.ErrorMessage, Does.Contain("offline"));
            Assert.That(detection.Success, Is.False);
            Assert.That(detection.ErrorMessage, Does.Contain("offline"));
            Assert.That(TimeUtility.StartUpdateTask().IsCompletedSuccessfully, Is.True);
        });
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("Character\u001fEncounter\u001fBoss — 日本語")]
    public void ManagedLocalIdsMatchExistingSha256Exactly(string input)
    {
        Assert.That(StableHash.Sha256(input), Is.EqualTo(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))));
    }

    [Test]
    public void ManagedLocalIdsMatchExistingSha256AcrossMultipleBlocks()
    {
        var input = new string('x', 10000);
        Assert.That(StableHash.Sha256(input), Is.EqualTo(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
