using System;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class MemberInfoViewModel
    {
        private SolidColorBrush _evenRow = (SolidColorBrush)App.Current.FindResource("Gray3Brush");
        private SolidColorBrush _oddRow = (SolidColorBrush)App.Current.FindResource("Gray4Brush");
        private string valueStringFormat = "#,##0";
        private string floatValueString = "0.00";
        public Entity _entity;
        private List<Combat> _info = new List<Combat>();
        private readonly SWTORClass _playerClass;

        public MemberInfoViewModel(int order, Entity e, List<Combat> info, List<OverlayType> selectedColumns)
        {
            _info = info;
            _entity = e;

            StatsSlots = new List<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i, Colors.WhiteSmoke, entity: _entity) { Value = GetValue(i) }));
            if (_entity != null)
            {
                IsLocalPlayer = e.IsLocalPlayer;
                _playerClass =
    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, info.Last().StartTime);
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, GetIconColorFromClass(_playerClass), _entity.Name, _playerClass.Name, IsLocalPlayer, _entity));
            }
            else
            {
                IsTotalsRow = true;
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, Colors.WhiteSmoke, "Totals"));
            }
            if (selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None, Colors.WhiteSmoke) { Value = "" });
        }

        public bool IsTotalsRow { get; set; }
    
        public bool IsLocalPlayer { get; set; }
        public string PlayerName => _entity?.Name;
        public Bitmap ClassIcon
        {
            get
            {
                var classIcon = StatsSlots.FirstOrDefault(s => s.Header == "Name")?.RoleIcon;
                if (classIcon != null)
                    return classIcon;
                return IconFactory._unknownIcon;
            }
        }

        public string ClassName => _playerClass?.Name + "/" + _playerClass?.Discipline;

        private string GetValue(OverlayType columnType)
        {
            var formatToUse = columnType == OverlayType.CleanseSpeed ? floatValueString : valueStringFormat;
            if (_entity == null)
                return MetricGetter.GetTotalforMetric(columnType, _info).ToString(formatToUse);
            return MetricGetter.GetValueForMetric(columnType, _info, _entity).ToString(formatToUse);
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
