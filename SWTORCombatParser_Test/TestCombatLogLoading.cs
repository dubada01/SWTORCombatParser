using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class TestCombatLogLoading
{
    private string _directory;
    private string _previousDirectory;
    private string _latest;

    [SetUp]
    public void SetUp()
    {
        _previousDirectory = CombatLogLoader.LoggingPath;
        _directory = Directory.CreateTempSubdirectory().FullName;
        CombatLogLoader.LoggingPath = _directory;
        var older = Path.Combine(_directory, "older.txt");
        _latest = Path.Combine(_directory, "newer.txt");
        File.WriteAllText(older, "first");
        File.WriteAllText(_latest, "second");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-1));
    }

    [TearDown]
    public void TearDown()
    {
        CombatLogLoader.LoggingPath = _previousDirectory;
        Directory.Delete(_directory, true);
    }

    [Test]
    public void TestLoadCombatLogs()
    {
        var files = CombatLogLoader.LoadAllCombatLogs();
        try { Assert.That(files.Select(f => f.Name), Is.EqualTo(new[] { "newer.txt", "older.txt" })); }
        finally { foreach (var file in files) file.Data.Dispose(); }
    }

    [Test]
    public void TestLoadMostRecentLog()
    {
        var file = CombatLogLoader.LoadMostRecentLog();
        using (file.Data) Assert.That(file.Path, Is.EqualTo(_latest));
    }

    [Test]
    public void TestLoadSpecifcLog()
    {
        var file = CombatLogLoader.LoadSpecificLog(_latest);
        using (file.Data) Assert.That(file.Data.ReadToEnd(), Is.EqualTo("second"));
    }
}
