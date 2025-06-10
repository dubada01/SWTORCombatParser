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
using SWTORCombatParser.Utilities.Converters;

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
        private SWTORClass _playerClass;
        private readonly OverlayTypeToReadableNameConverter _nameConverter;

        public MemberInfoViewModel(int order, Entity e, List<Combat> info, List<OverlayType> selectedColumns)
        {
            _nameConverter = new OverlayTypeToReadableNameConverter();
            _info = info;
            _entity = e;

            StatsSlots = new List<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i, entity: _entity) { Value = GetValue(i) }));
            if (_entity != null)
            {
                IsLocalPlayer = e.IsLocalPlayer;
                _playerClass =
    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, info.Last().StartTime);
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, _entity.Name, _playerClass.Discipline, IsLocalPlayer, _entity));
            }
            else
            {
                IsTotalsRow = true;
                StatsSlots.Insert(0, new StatsSlotViewModel(OverlayType.None, "Totals"));
            }
            if (selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None) { Value = "" });
        }
        public void Update(List<Combat> newInfo, List<OverlayType> selectedColumns)
        {
            _info = newInfo;
            if (_entity != null)
            {
                IsTotalsRow = false;
                IsLocalPlayer = _entity.IsLocalPlayer;
                _playerClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, newInfo.Last().StartTime);

                foreach (var column in selectedColumns)
                {
                    if(!StatsSlots.Any(s=>s.Header == _nameConverter.Convert(column,null,null,System.Globalization.CultureInfo.InvariantCulture).ToString()))
                    {
                        StatsSlots.Insert(selectedColumns.IndexOf(column),new StatsSlotViewModel(column,entity:_entity){Value = GetValue(column)});
                    }
                }
                for (var columnIndex = 0; columnIndex < StatsSlots.Count; columnIndex++)
                {
                    var column = StatsSlots[columnIndex];
                    if (column.Header != "Name" && selectedColumns.All(c => column.OverlayType != c))
                    {
                        StatsSlots.Remove(column);
                    }
                    else
                    {
                        column.Value = GetValue(column.OverlayType);
                    }
                }
            }
            if (selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None) { Value = "" });
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

        public List<StatsSlotViewModel> StatsSlots { get; set; } = new List<StatsSlotViewModel>();
    }
}
