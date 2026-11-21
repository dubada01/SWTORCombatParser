using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class ChallengeConfigRaceTests
{
    private readonly Dictionary<FieldInfo, object> _original = new();
    private string _directory;
    private string _config;

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory().FullName;
        _config = Path.Combine(_directory, "challengeInfo.json");
        foreach (var (name, path) in new[] { ("appDataPath", _directory), ("infoPath", _config),
                     ("activePath", Path.Combine(_directory, "challengeActive.json")) })
        {
            var field = typeof(DefaultChallengeManager).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            _original[field] = field.GetValue(null)!;
            field.SetValue(null, path);
        }
        DefaultChallengeManager.Init();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var pair in _original) pair.Key.SetValue(null, pair.Value);
        _original.Clear();
        Directory.Delete(_directory, true);
    }

    [Test]
    public void StartupRefreshPreservesConcurrentUserChangesAndLegacyColor()
    {
        var id = Guid.NewGuid();
        DefaultChallengeData Source(int revision) => new()
        {
            ChallengeSource = "Shared",
            Challenges = new() { new Challenge { Id = id, Source = "Shared", IsBuiltIn = true,
                BuiltInRev = revision, BackgroundColor = "1,2,3" } }
        };
        var old = Source(1);
        DefaultChallengeManager.AddSource(old);
        Parallel.Invoke(
            () => DefaultChallengeManager.RefreshBuiltIns(2, new() { Source(2) }),
            () => DefaultChallengeManager.SetChallengeEnabled(true, old.Challenges.Single()),
            () => DefaultChallengeManager.AddSource(new DefaultChallengeData { ChallengeSource = "Custom" }));
        var all = DefaultChallengeManager.GetAllDefaults();
        Assert.That(all.Select(x => x.ChallengeSource), Is.EquivalentTo(new[] { "Shared", "Custom" }));
        var challenge = all.Single(x => x.ChallengeSource == "Shared").Challenges.Single();
        Assert.That(challenge.IsEnabled, Is.True);
        Assert.That(challenge.BuiltInRev, Is.EqualTo(2));
        var saved = JArray.Parse(File.ReadAllText(_config)).Single(x => (string)x["ChallengeSource"] == "Shared");
        Assert.That((string)saved["Challenges"][0]["BackgroundBrush"], Is.EqualTo("#ff010203").IgnoreCase);
    }

    [Test]
    [Platform("Win")]
    public async Task SaveSurvivesAReaderHoldingTheDestinationOpen()
    {
        Task writer;
        bool staged;
        using (File.Open(_config, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            writer = Task.Run(() => DefaultChallengeManager.AddSource(new() { ChallengeSource = "Shared" }));
            staged = SpinWait.SpinUntil(() => File.Exists(_config + ".temp"), TimeSpan.FromSeconds(3));
        }
        await writer.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(staged, Is.True);
        Assert.That(DefaultChallengeManager.GetAllDefaults().Single().ChallengeSource, Is.EqualTo("Shared"));
    }
}
