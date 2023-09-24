using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Model.Timers;

public class AbsorbShieldManager
{
    private Entity _target;
    public AbsorbShieldManager(Entity target)
    {
        _target = target;
    }

    public double CheckForDamage(ParsedLogEntry log)
    {
        if (log.Target.LogId == _target.LogId && log.Effect.EffectType == EffectType.Apply &&
            log.Effect.EffectId == _7_0LogParsing._damageEffectId)
        {
            return log.Value.DblValue;
        }

        return 0;
    }
}