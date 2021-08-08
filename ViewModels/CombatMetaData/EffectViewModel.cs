using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public class EffectViewModel
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public double Duration { get; set; }
        public int Count { get; set; }
    }
}
