using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class CharacterClassHelper
    {
        public static SWTORClass GetClassFromEntityAtTime(Entity entity, DateTime time)
        {
            if (!CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(entity))
                return new SWTORClass();
            var classOfSource = CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo[entity];
            if (classOfSource == null)
                return new SWTORClass();
            var classAtTime = classOfSource[classOfSource.Keys.ToList().MinBy(v => (time - v).TotalSeconds).First()];
            return classAtTime;
        }
    }
}
