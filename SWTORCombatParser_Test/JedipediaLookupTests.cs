using NUnit.Framework;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser_Test;

public class JedipediaLookupTests
{
    [Test]
    public void AbilityAndEffectResolveToTheKnownOwningAbility()
    {
        // Jedipedia's Force Lightning entry lists these combat-log IDs.
        const string lightning = "https://swtor.jedipedia.net/en/abl/808252715565056";
        Assert.That(JedipediaLookup.Ability(808252715565056)?.AbsoluteUri, Is.EqualTo(lightning));
        Assert.That(JedipediaLookup.Effect(808252715565423)?.AbsoluteUri, Is.EqualTo(lightning));
    }

    [TestCase(0UL)]
    [TestCase(836045448945501UL)] // Damage
    [TestCase(836045448945479UL)] // AbilityActivate
    [TestCase(836045448945493UL)] // Death
    public void GenericOrMissingEffectsDoNotProduceBrokenLinks(ulong id) => Assert.That(JedipediaLookup.Effect(id), Is.Null);

    [Test]
    public void MissingAbilityDoesNotOpenTheDatabaseHomePage() => Assert.That(JedipediaLookup.Ability(0), Is.Null);
}
