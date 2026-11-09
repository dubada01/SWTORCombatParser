using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities.MouseHandler;

namespace SWTORCombatParser_Test;

public class ManualRaidFrameAlignmentTests
{
    private static readonly DateTime Start = new(2026, 11, 8, 12, 0, 0);

    [TestCase(true)]
    [TestCase(false)]
    [NonParallelizable]
    public void ParsedCombatLogClearThenSetAssignsOnlyNewPlayer(bool clearIncludesPreviousPlayer)
    {
        // Preserve the parser's caches and date because other fixtures share these statics.
        var fields = new[] { "_dateTime", "_interner", "valueRegex", "threatRegex", "_fileEncoding", "_currentEntities" }
            .Select(name => typeof(_7_0LogParsing).GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Append(typeof(CombatLogParser).GetField("_fileEncoding", BindingFlags.Static | BindingFlags.NonPublic));
        var prior = fields.ToDictionary(field => field, field => field.GetValue(null));
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _7_0LogParsing.SetupRegex();
            CombatLogParser.SetParseDate(Start);
            typeof(_7_0LogParsing).GetMethod("ResetEntities", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            const string source = "[@AlignmentTester#923456789|(0,0,0,0)|(100/100)]";
            var oldTarget = clearIncludesPreviousPlayer ? "[@Oldplayer#923456788|(0,0,0,0)|(100/100)]" : "[]";
            var clear = CombatLogParser.ParseLine(
                $"[12:00:00.025] {source} {oldTarget} [] [Event {{836045448945472}}: TargetCleared {{836045448953669}}]",
                0, Start, false);
            var select = CombatLogParser.ParseLine(
                $"[12:00:00.050] {source} [@Newplayer#923456787|(0,0,0,0)|(100/100)] [] [Event {{836045448945472}}: TargetSet {{836045448953668}}]",
                1, clear.TimeStamp, false);
            // Live parsing identifies the local player from the preceding AreaEntered record.
            clear.Source.IsLocalPlayer = true;
            var alignment = new ManualRaidFrameAlignment();
            alignment.RecordClick(2, 1, Start);

            Assert.Multiple(() =>
            {
                Assert.That(clear.Error, Is.EqualTo(ErrorType.None));
                Assert.That(select.Error, Is.EqualTo(ErrorType.None));
                Assert.That(clear.Effect.EffectType, Is.EqualTo(EffectType.TargetChanged));
                Assert.That(select.Effect.EffectType, Is.EqualTo(EffectType.TargetChanged));
                Assert.That(select.TimeStamp, Is.EqualTo(Start.AddMilliseconds(50)));
                Assert.That(select.Source.IsLocalPlayer, Is.True);
            });
            Assert.That(alignment.TryMatch(clear, out _), Is.False);
            Assert.That(alignment.TryMatch(select, out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(new RaidFrameCell(2, 1)));
            Assert.That(select.Target.Name, Is.EqualTo("Newplayer"));
        }
        finally
        {
            foreach (var (field, value) in prior)
                field.SetValue(null, value);
        }
    }

    [Test]
    public void ClearPreviousTargetDoesNotConsumeClickBeforeNewSelection()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(2, 1, Start);
        var clear = Selection("Previous player", _7_0LogParsing.TargetClearedId);
        var select = Selection("New player", _7_0LogParsing.TargetSetId);

        Assert.That(alignment.TryMatch(clear, out _), Is.False);
        Assert.That(alignment.HasPendingClick, Is.True);
        Assert.That(alignment.TryMatch(select, out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(2, 1)));
    }

    [Test]
    public void OtherPlayersSelectionAndNonPlayerTargetsAreIgnored()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(2, 1, Start);
        var otherPlayer = Selection("Player", _7_0LogParsing.TargetSetId);
        otherPlayer.Source.IsLocalPlayer = false;
        var enemy = Selection("Enemy", _7_0LogParsing.TargetSetId);
        enemy.Target.IsCharacter = false;

        Assert.That(alignment.TryMatch(otherPlayer, out _), Is.False);
        Assert.That(alignment.TryMatch(enemy, out _), Is.False);
        Assert.That(alignment.HasPendingClick, Is.True);
    }

    [Test]
    public void IncompleteSelectionDoesNotConsumePendingClick()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start);
        var unnamed = Selection("", _7_0LogParsing.TargetSetId);

        Assert.That(alignment.TryMatch((ParsedLogEntry)null, out _), Is.False);
        Assert.That(alignment.TryMatch(new ParsedLogEntry(), out _), Is.False);
        Assert.That(alignment.TryMatch(unnamed, out _), Is.False);
        Assert.That(alignment.HasPendingClick, Is.True);
    }

    [Test]
    public void SelectionMatchesClickedCellOnlyOnce()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(2, 1, Start);

        Assert.That(alignment.TryMatch(Start.AddMilliseconds(50), out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(2, 1)));
        Assert.That(alignment.TryMatch(Start.AddMilliseconds(100), out _), Is.False);
        Assert.That(alignment.HasPendingClick, Is.False);
    }

    [Test]
    public void ClickWithoutSelectionDoesNotShiftNextPlayersAssignment()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start); // Already selected: the game writes no new target entry.
        alignment.RecordClick(1, 1, Start.AddSeconds(1));

        Assert.That(alignment.TryMatch(Start.AddMilliseconds(1050), out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(1, 1)));
    }

    [Test]
    public void DelayedEarlierSelectionDoesNotClaimNewerClick()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start);
        alignment.RecordClick(1, 1, Start.AddMilliseconds(100));

        Assert.That(alignment.TryMatch(Start.AddMilliseconds(99), out _), Is.False);
        Assert.That(alignment.PendingCell, Is.EqualTo(new RaidFrameCell(1, 1)));
        Assert.That(alignment.TryMatch(Start.AddMilliseconds(150), out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(1, 1)));
    }

    [Test]
    public void SelectionAtSameTimestampAsClickCanMatch()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 1, Start);

        Assert.That(alignment.TryMatch(Start, out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(0, 1)));
    }

    [Test]
    public void TimeoutRejectsSelectionEvenBeforeExpiryTimerRuns()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start);

        Assert.That(alignment.TryMatch(Start + ManualRaidFrameAlignment.SelectionTimeout, out _), Is.False);
        Assert.That(alignment.HasPendingClick, Is.False);
    }

    [Test]
    public void RepeatedClickRestartsTimeoutWithoutOldExpiryClearingNewClick()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start);
        alignment.RecordClick(0, 0, Start.AddSeconds(3));

        Assert.That(alignment.Expire(Start.AddSeconds(4)), Is.False);
        Assert.That(alignment.TryMatch(Start.AddSeconds(5), out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(0, 0)));
        Assert.That(alignment.Expire(Start.AddSeconds(8)), Is.False);
    }

    [Test]
    public void ExpirationIsReportedOnlyOnce()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(0, 0, Start);

        Assert.That(alignment.Expire(Start.AddSeconds(3.999)), Is.False);
        Assert.That(alignment.Expire(Start.AddSeconds(4)), Is.True);
        Assert.That(alignment.Expire(Start.AddSeconds(5)), Is.False);
        Assert.That(alignment.TryMatch(Start.AddSeconds(2), out _), Is.False);
    }

    [Test]
    public void EndingOrRestartingSetupDiscardsPendingSelection()
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(2, 1, Start);
        alignment.Clear();

        Assert.That(alignment.TryMatch(Start.AddMilliseconds(100), out _), Is.False);
        alignment.RecordClick(3, 0, Start.AddSeconds(1));
        Assert.That(alignment.TryMatch(Start.AddMilliseconds(1100), out var cell), Is.True);
        Assert.That(cell, Is.EqualTo(new RaidFrameCell(3, 0)));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    public void InvalidClickDoesNotReplacePendingCell(int row, int column)
    {
        var alignment = new ManualRaidFrameAlignment();
        alignment.RecordClick(1, 1, Start);

        Assert.That(alignment.RecordClick(row, column, Start.AddMilliseconds(1)), Is.False);
        Assert.That(alignment.PendingCell, Is.EqualTo(new RaidFrameCell(1, 1)));
    }

    [Test]
    public void WindowsMouseHookUsesNativePointLayoutAndSignedScreenCoordinates()
    {
        var hookType = typeof(MouseHookHandler).GetNestedType("MSLLHOOKSTRUCT", BindingFlags.NonPublic);
        Assert.That(hookType, Is.Not.Null);
        Assert.That(Marshal.OffsetOf(hookType, "mouseData").ToInt32(), Is.EqualTo(8));
        Assert.That(Marshal.OffsetOf(hookType, "time").ToInt32(), Is.EqualTo(16));
        Assert.That(Marshal.SizeOf(hookType), Is.EqualTo(IntPtr.Size == 8 ? 32 : 24));

        var memory = Marshal.AllocHGlobal(Marshal.SizeOf(hookType));
        try
        {
            Marshal.Copy(new byte[Marshal.SizeOf(hookType)], 0, memory, Marshal.SizeOf(hookType));
            Marshal.WriteInt32(memory, 0, -1920);
            Marshal.WriteInt32(memory, 4, 450);
            var hook = Marshal.PtrToStructure(memory, hookType);
            var point = hookType.GetField("pt").GetValue(hook);

            Assert.That(point.GetType().GetField("X").GetValue(point), Is.EqualTo(-1920));
            Assert.That(point.GetType().GetField("Y").GetValue(point), Is.EqualTo(450));
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static ParsedLogEntry Selection(string target, ulong effectId) => new()
    {
        TimeStamp = Start.AddMilliseconds(50),
        SourceInfo = new EntityInfo { Entity = new Entity { Name = "Local", IsLocalPlayer = true, IsCharacter = true } },
        TargetInfo = new EntityInfo { Entity = new Entity { Name = target, IsCharacter = true } },
        Effect = new Effect { EffectType = EffectType.TargetChanged, EffectId = effectId }
    };
}
