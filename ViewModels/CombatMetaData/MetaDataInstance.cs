using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public class MetaDataInstance
    {
        public string Category { get; set; }
        public System.Windows.Media.Color Color { get; set; }
        public string TotalLabel { get; set; }
        public string TotalValue { get; set; }
        public string MaxLabel { get; set; }
        public string MaxValue { get; set; }
        public string RateLabel { get; set; }
        public string RateValue { get; set; }
        public string EffectiveTotalLabel { get; set; }
        public string EffectiveTotalValue { get; set; }
        public string EffectiveMaxLabel { get; set; }
        public string EffectiveMaxValue { get; set; }
        public string EffectiveRateLabel { get; set; }
        public string EffectiveRateValue { get; set; }
    }
}
