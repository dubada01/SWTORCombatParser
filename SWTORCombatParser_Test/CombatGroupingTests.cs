using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser_Test;

public class CombatGroupingTests
{
    private static IEnumerable<ParsedLogEntry> OnlyOnce(IEnumerable<ParsedLogEntry> logs)
    {
        var enumerated = false;
        return Enumerate();

        IEnumerable<ParsedLogEntry> Enumerate()
        {
            if (enumerated)
                throw new System.InvalidOperationException("The input was enumerated more than once.");
            enumerated = true;
            foreach (var log in logs)
                yield return log;
        }
    }

    [Test]
    public void GroupsLogsByNameAndAbilityInOnePass()
    {
        var source1 = new Entity { Name = "Player", LogId = 1 };
        var source2 = new Entity { Name = "Player", LogId = 2 };
        var target = new Entity { Name = "Target", LogId = 3 };
        var logs = new[]
        {
            new ParsedLogEntry { SourceInfo = new EntityInfo { Entity = source1 }, TargetInfo = new EntityInfo { Entity = target }, Ability = "Hit" },
            new ParsedLogEntry { SourceInfo = new EntityInfo { Entity = source2 }, TargetInfo = new EntityInfo { Entity = target }, Ability = "Hit" },
            new ParsedLogEntry { SourceInfo = new EntityInfo { Entity = source1 }, TargetInfo = new EntityInfo { Entity = target }, Ability = "Heal" }
        };
        var combat = new Combat();

        var bySource = combat.GetBySource(OnlyOnce(logs));
        Assert.That(bySource[source1].Count, Is.EqualTo(2));
        Assert.That(bySource[source2].Count, Is.EqualTo(1));
        Assert.That(combat.GetBySourceName(OnlyOnce(logs)).Single().Value.ToArray(), Is.EqualTo(logs));
        Assert.That(combat.GetByTargetName(OnlyOnce(logs)).Single().Value.ToArray(), Is.EqualTo(logs));
        Assert.That(combat.GetByAbility(OnlyOnce(logs))["Hit"].ToArray(), Is.EqualTo(logs.Take(2)));
    }
}
