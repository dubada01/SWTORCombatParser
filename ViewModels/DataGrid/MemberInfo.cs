using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private Combat? _info;
        private SWTORClass _playerClass;
        private readonly OverlayTypeToReadableNameConverter _nameConverter;

        public MemberInfoViewModel(int order, Entity e, Combat info, List<OverlayType> selectedColumns)
        {
            _nameConverter = new OverlayTypeToReadableNameConverter();
            _info = info;
            _entity = e;

            StatsSlots = new ObservableCollection<StatsSlotViewModel>(selectedColumns.Select(i => new StatsSlotViewModel(i, entity: _entity) { Value = GetValue(i) }));
            if (_entity != null)
            {
                IsLocalPlayer = e.IsLocalPlayer;
                _playerClass =
    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, info.StartTime);
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

        public void Update(Combat newInfo, List<OverlayType> selectedColumns)
        {
            _info = newInfo;
            if (_entity != null)
            {
                IsTotalsRow = false;
                IsLocalPlayer = _entity.IsLocalPlayer;
                var currentClass =
                    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(_entity, newInfo.StartTime);
                var classChanged = false;
                if (currentClass != _playerClass)
                {
                    _playerClass = currentClass;
                    classChanged = true;
                }


                foreach (var column in selectedColumns)
                {
                    if (!StatsSlots.Any(s =>
                            s.Header == _nameConverter.Convert(column, null, null,
                                System.Globalization.CultureInfo.InvariantCulture).ToString()))
                    {
                        StatsSlots.Insert(selectedColumns.IndexOf(column),
                            new StatsSlotViewModel(column, entity: _entity) { Value = GetValue(column) });
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
                        if (column.Header == "Name" && classChanged)
                            column.UpdateIcon(_playerClass.Discipline);
                        column.Value = GetValue(column.OverlayType);
                    }
                }
            }

            // ──────────────────────────────────────────────────────────────
//  RE-ORDER existing slots to match selectedColumns
//  (keep index 0 for the "Name" column, ignore padding slots)
// ──────────────────────────────────────────────────────────────
            void ReorderSlots()
            {
                // 1.  Build a quick lookup of where each OverlayType *should* live
                //     desiredIndex = 1 + position in selectedColumns
                var targetIndexByType = selectedColumns
                    .Select((c, i) => (c, idx: i + 1)) // +1 because 0 = Name
                    .ToDictionary(t => t.c, t => t.idx);

                // 2.  Walk the collection once and move misplaced items
                //     ObservableCollection<T> has a built-in Move()
                for (int cur = 1; cur < StatsSlots.Count; cur++) // skip Name at 0
                {
                    var slot = StatsSlots[cur];
                    if (slot.OverlayType == OverlayType.None) // ignore padding
                        continue;

                    var want = targetIndexByType.TryGetValue(slot.OverlayType, out var idx)
                        ? idx
                        : -1; // -1 ⇒ no longer selected

                    if (want == -1)
                        continue; // removal handled above

                    if (cur != want)
                    {
                        // ObservableCollection<T>.Move keeps change notifications concise
                        if (StatsSlots is ObservableCollection<StatsSlotViewModel> oc)
                            oc.Move(cur, want);
                        else
                        {
                            // fallback for plain List<T>
                            StatsSlots.RemoveAt(cur);
                            StatsSlots.Insert(want, slot);
                        }

                        // we just moved 'slot' to 'want'; adjust loop index
                        // so the element that was at 'want' isn’t skipped
                        if (cur < want)
                            cur--;
                    }
                }
            }

// call it right before you pad with blanks / exit the method:
            ReorderSlots();

// keep your existing padding logic
            if (selectedColumns.Count < 10)
                StatsSlots.Add(new StatsSlotViewModel(OverlayType.None) { Value = "" });
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

        public ObservableCollection<StatsSlotViewModel> StatsSlots { get; set; } = new();
    }
}
