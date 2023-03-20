using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class MemberInfoViewModel
    {
        private SolidColorBrush _evenRow = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100));
        private SolidColorBrush _oddRow = new SolidColorBrush(Color.FromArgb(255, 90, 90, 90));
        private string valueStringFormat = "#,##0";
        public Entity _entity;
        private List<Combat> _info;
        public MemberInfoViewModel(int order, Entity e, List<Combat> info, List<OverlayType> selectedColumns)
        {
            _info = info;
            _entity = e;
            IsLocalPlayer = e.IsLocalPlayer;
            StatsSlots = new List<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i,Colors.WhiteSmoke) {  Value = GetValue(i) }));
            var playerClass =
                CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, info.Last().StartTime);
            StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None,GetIconColorFromClass(playerClass), _entity.Name, playerClass.Name,IsLocalPlayer));
            if(selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None,Colors.WhiteSmoke) { Value = "" });
        }
        public bool IsLocalPlayer { get; set; }
        public void AssignBackground(int position)
        {
            foreach(var slot in StatsSlots)
            {
                slot.BackgroundColor = position % 2 == 0 ? _evenRow : _oddRow;
            }
        }
        private string GetValue(OverlayType columnType)
        {
           return MetricGetter.GetValueForMetric(columnType, _info, _entity).ToString(valueStringFormat);
        }
        private Color GetIconColorFromClass(SWTORClass classInfo)
        {
            return classInfo.Role switch
            {
                Role.Healer => Colors.ForestGreen,
                Role.Tank => Colors.CornflowerBlue,
                Role.DPS =>Colors.IndianRed,
                _ => (Color)ResourceFinder.GetColorFromResourceName("Gray4")
            };
        }
        public List<StatsSlotViewModel> StatsSlots { get; set; } = new List<StatsSlotViewModel>();
    }
}
