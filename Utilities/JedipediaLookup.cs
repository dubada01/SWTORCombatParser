using System;
using System.Globalization;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Utilities;

public static class JedipediaLookup
{
    // Combat effect IDs retain the owning ability in their upper 32 bits.
    private const ulong AbilityMask = 0xFFFFFFFF00000000UL;

    public static Uri? Ability(ulong id) => (id & AbilityMask) == 0 ? null :
        new Uri("https://swtor.jedipedia.net/en/abl/" + id.ToString(CultureInfo.InvariantCulture));

    public static Uri? Effect(ulong id)
    {
        var abilityId = id & AbilityMask;
        // Damage, healing, activation, death, etc. are engine events, not abilities.
        return abilityId == (_7_0LogParsing._damageEffectId & AbilityMask) ? null : Ability(abilityId);
    }
}
