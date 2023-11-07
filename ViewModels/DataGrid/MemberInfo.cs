using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class MemberInfoViewModel
    {
        private SolidColorBrush _evenRow = (SolidColorBrush)App.Current.FindResource("Gray3Brush");
        private SolidColorBrush _oddRow = (SolidColorBrush)App.Current.FindResource("Gray4Brush");
        private string valueStringFormat = "#,##0";
        public Entity _entity;
        private List<Combat> _info = new List<Combat>();

        public MemberInfoViewModel(int order, Entity e, List<Combat> info, List<OverlayType> selectedColumns)
        {
            _info = info;
            _entity = e;
            
            StatsSlots = new List<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i, Colors.WhiteSmoke) { Value = GetValue(i) }));
            if(_entity != null)
            {
                IsLocalPlayer = e.IsLocalPlayer;
                var playerClass =
    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, info.Last().StartTime);
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, GetIconColorFromClass(playerClass), _entity.Name, playerClass.Name, IsLocalPlayer));
            }
            else
            {
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, Colors.WhiteSmoke, "Totals"));
            }
            if (selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None, Colors.WhiteSmoke) { Value = "" });
        }
        public bool IsLocalPlayer { get; set; }
        public void AssignBackground(int position)
        {
            foreach (var slot in StatsSlots)
            {
                slot.BackgroundColor = position % 2 == 0 ? _evenRow : _oddRow;
            }
        }
        private string GetValue(OverlayType columnType)
        {
            if (_entity == null)
                return MetricGetter.GetTotalforMetric(columnType, _info).ToString(valueStringFormat);
            return MetricGetter.GetValueForMetric(columnType, _info, _entity).ToString(valueStringFormat);
        }
        private Color GetIconColorFromClass(SWTORClass classInfo)
        {
            return classInfo.Role switch
            {
                Role.Healer => Colors.ForestGreen,
                Role.Tank => Colors.CornflowerBlue,
                Role.DPS => Colors.IndianRed,
                _ => (Color)ResourceFinder.GetColorFromResourceName("Gray4")
            };
        }
        public List<StatsSlotViewModel> StatsSlots { get; set; } = new List<StatsSlotViewModel>();
    }
}
