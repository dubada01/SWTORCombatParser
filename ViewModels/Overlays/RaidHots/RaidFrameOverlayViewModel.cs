//using MoreLinq;
using Microsoft.VisualBasic.Logging;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidFrameOverlayViewModel : INotifyPropertyChanged
    {
        private Entity _mostRecentTargetedPlayer;
        private DateTime _mostRecentTargetTime;
        private Point _mostRecentlyClickedCell;
        private DateTime _mostRecentClickTime;
        private bool _isMouseInFrame;
        private int rows;
        private int columns;
        private bool editable;
        private bool active;
        private RaidFrameOverlay _view;
        private bool _conversationActive;
        private bool _inCombat;
        private bool _usingDecreasedAccuracy;
        private List<long> _validBossIds = new List<long>();
        public bool Editable
        {
            get => editable;
            set
            {
                editable = value;
                ToggleLocked(!editable);
                OnPropertyChanged();
            }
        }
        public bool Active
        {
            get => active; set
            {
                active = value;
            }
        }
        public System.Drawing.Point TopLeft { get; set; }
        public int Width { get; set; }
        public double ScreenWidth { get; set; }
        public double ColumnWidth => ScreenWidth / Columns;
        public int Height { get; set; }
        public double ScreenHeight { get; set; }
        public double RowHeight => ScreenHeight / Rows;

        public List<PlacedName> CurrentNames = new List<PlacedName>();
        public bool SizeSet = false;
        public event Action<bool> ToggleLocked = delegate { };
        public event Action<string> PlayerChanged = delegate { };
        public RaidFrameOverlayViewModel(RaidFrameOverlay view)
        {
            _view = view;
            _view.AreaClicked += CellClicked;
            _view.MouseInArea += MouseInArea;
            TimerController.TimerTriggered += CheckForRaidHOT;
            TimerController.TimerTriggered += CheckForDefensive;
            CombatLogStreamer.CombatUpdated += OnStartCombat;
            CombatLogStreamer.NewLineStreamed += CheckForTargetChanged;
            CombatLogStreamer.HistoricalLogsFinished += SetCurrentEncounter;
            CombatLogStateBuilder.AreaEntered += NewEncounterEntered;
        }



        private void OnStartCombat(CombatStatusUpdate update)
        {
            if(update.Type == UpdateType.Start)
            {
                _inCombat = true;
                if (_conversationActive)
                {
                    _view.Show();
                    _conversationActive = false;
                }
            }
            else
            {
                _inCombat = false;
            }

        }

        private void SetCurrentEncounter(DateTime arg1, bool arg2)
        {
            NewEncounterEntered(CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(arg1));
        }

        private void NewEncounterEntered(EncounterInfo obj)
        {
            _validBossIds = obj.BossIds.Any() ? obj.BossIds.SelectMany(kvp => kvp.Value.SelectMany(kvp => kvp.Value)).ToList() : new List<long>();
        }
        private void MouseInArea(bool obj)
        {
            _isMouseInFrame = obj;
        }
        public void CellClicked(double xFract, double yFract)
        {
            var cellX = (int)(xFract * columns);
            var cellY = (int)(yFract * rows);
            _mostRecentlyClickedCell = new Point(cellX, cellY);
            _mostRecentClickTime = DateTime.Now;
        }
        private void CheckForTargetChanged(ParsedLogEntry obj)
        {
            if (obj.Effect.EffectType == EffectType.TargetChanged)
            {
                foreach (var bossId in _validBossIds)
                {
                    if (obj.Source.LogId == bossId)
                    {
                        ResetCellPreviouslyTargeted();
                        if (obj.Effect.EffectId == _7_0LogParsing.TargetSetId)
                        {

                            var cellToUpdate = GetCellThatMatchesName(obj.Target.Name);
                            if (cellToUpdate == null)
                                return;
                            cellToUpdate.IsTargeted = true;
                            cellToUpdate.TargetedBy = bossId;
                        }

                    }
                }
            }
            CheckForConversation(obj);
            CorrectCellWhenTargeted(obj);
        }
        private void CorrectCellWhenTargeted(ParsedLogEntry obj)
        {
            if(_inCombat)
                return; 
//#if DEBUG
//            //DELETE THIS
//            var orbsTestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Orbs Dev Stuff");
//            var clickHistoryPath = Path.Combine(orbsTestDir, "characterSelectHistory.txt");
//            if (!Directory.Exists(orbsTestDir))
//            {
//                Directory.CreateDirectory(orbsTestDir);
//            }
//            if (!File.Exists(clickHistoryPath))
//            {
//                File.Create(clickHistoryPath).Close();
//            }
//            var currentHistory = File.ReadAllLines(clickHistoryPath).ToList();
//            currentHistory.Add($"Time: {_mostRecentClickTime} - Cell: ({_mostRecentlyClickedCell.X},{_mostRecentlyClickedCell.Y}) - Player: {obj.Target.Name}");
//            File.WriteAllLines(clickHistoryPath, currentHistory);
//            //
//#endif
            if (!obj.Source.IsLocalPlayer || obj.Effect.EffectType != EffectType.TargetChanged || !obj.Target.IsCharacter || !_isMouseInFrame) return;
            var cellClicked = CurrentNames.FirstOrDefault(n => n.Row == _mostRecentlyClickedCell.Y && n.Column == _mostRecentlyClickedCell.X);
            if (cellClicked != null)
            {
                if (AreNamesCloseEnough(cellClicked.Name.ToLower(), GetNameWithoutSpecials(obj.Target.Name).ToLower(),4))
                {
//#if DEBUG
//                    //DELETE THIS
//                    currentHistory = File.ReadAllLines(clickHistoryPath).ToList();
//                    currentHistory.Add($"Someone close to {obj.Target.Name} already found");
//                    File.WriteAllLines(clickHistoryPath, currentHistory);
//                    //
//#endif
                    return;
                }
//#if DEBUG
//                //DELETE THIS
//                currentHistory = File.ReadAllLines(clickHistoryPath).ToList();
//                currentHistory.Add($"Updating the cell at ({_mostRecentlyClickedCell.X},{_mostRecentlyClickedCell.Y}). Replacing {cellClicked.Name} with {obj.Target.Name}");
//                File.WriteAllLines(clickHistoryPath, currentHistory);
//                //
//#endif
                cellClicked.Name = obj.Target.Name;
            }
            else
            {
//#if DEBUG
//                //DELETE THIS
//                currentHistory = File.ReadAllLines(clickHistoryPath).ToList();
//                currentHistory.Add($"Adding {obj.Target.Name} to {JsonConvert.SerializeObject(CurrentNames)}");
//                File.WriteAllLines(clickHistoryPath, currentHistory);
//                //
//#endif
                CurrentNames.Add(new PlacedName { Column = (int)_mostRecentlyClickedCell.X, Row = (int)_mostRecentlyClickedCell.Y, Name = obj.Target.Name });
            }
            UpdateCells();
        }

        private void CheckForConversation(ParsedLogEntry obj)
        {
            if (!obj.Source.IsLocalPlayer)
                return;
            if (obj.Effect.EffectId == "806968520343876" && obj.Effect.EffectType == EffectType.Apply && Active)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _conversationActive = true;
                    _view.Hide();
                });
            }
            if (obj.Effect.EffectId == "806968520343876" && obj.Effect.EffectType == EffectType.Remove && Active)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _conversationActive = false;
                    _view.Show();
                });
            }
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!obj.SourceTimer.IsHot || !CurrentNames.Any() || obj.TargetAddendem == null ||
                obj.SourceTimer.IsSubTimer || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }

            var playerName = obj.TargetAddendem.ToLower();
            var cellToUpdate = GetCellThatMatchesName(playerName);
            if (cellToUpdate == null || cellToUpdate.AlreadyHasTimer(obj.TimerName))
                return;
            cellToUpdate.AddHOT(obj);
            callback(obj);
        }
        private void CheckForDefensive(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!obj.SourceTimer.IsBuiltInDefensive || !CurrentNames.Any() || obj.TargetAddendem == null ||
                obj.SourceTimer.IsSubTimer || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }

            var playerName = obj.TargetAddendem.ToLower();
            var cellToUpdate = GetCellThatMatchesName(playerName);
            if (cellToUpdate == null || cellToUpdate.AlreadyHasTimer(obj.TimerName))
                return;
            var currentRole = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(playerName,DateTime.Now).Role;
            if (currentRole != DataStructures.ClassInfos.Role.Tank)
            {
                callback(obj);
                return;
            }

            cellToUpdate.AddDCD(obj);
            callback(obj);
        }
        public void Reset()
        {
            foreach (var cell in RaidHotCells)
            {
                cell.Reset();
            }
        }
        private RaidHotCell GetCellThatMatchesName(string playerName)
        {
            var normalName = GetNameWithoutSpecials(playerName);

            var bestMatch = CurrentNames.Select(n => n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s, normalName));
            if (!AreNamesCloseEnough(normalName, bestMatch,4))
                return null;
            return RaidHotCells.MinBy(s => LevenshteinDistance.Compute(s.Name.ToLower(), bestMatch));
        }

        private bool AreNamesCloseEnough(string normalName, string bestMatch,int threshold)
        {
            return LevenshteinDistance.Compute(bestMatch, normalName) < threshold || _usingDecreasedAccuracy;
        }

        private void ResetCellPreviouslyTargeted()
        {
            RaidHotCells.ForEach(c =>
            {
                c.TargetedBy = 0;
                c.IsTargeted = false;
            });
        }
        private string GetNameWithoutSpecials(string name)
        {
            return new string(name.Normalize(NormalizationForm.FormD)
                .ToCharArray()
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Replace("\'", "").Replace("-", "");
        }


        public void UpdateNames(List<PlacedName> orderedNames)
        {
            CurrentNames = orderedNames;

            UpdateCells();
        }

        public void SetTextMatchAccuracy(bool useLowAccuracy)
        {
            _usingDecreasedAccuracy = useLowAccuracy;
        }
        public int Rows
        {
            get => rows; set
            {
                rows = value;
                OnPropertyChanged();
                OnPropertyChanged("RowHeight");
                UpdateCells();
            }
        }
        public int Columns
        {
            get => columns; set
            {
                columns = value;
                OnPropertyChanged();
                OnPropertyChanged("ColumnWidth");
                UpdateCells();
            }
        }
        private object _cellUpdateLock = new object();

        private void UpdateCells()
        {
            lock (_cellUpdateLock)
            {
                if (!CurrentNames.Any() || RaidHotCells.Count != (Rows * Columns))
                {
                    InitRaidCells();
                    RaidHotCells = RaidHotCells.OrderBy(c => c.Row * Columns + c.Column).ToList();
                    OnPropertyChanged("RaidHotCells");
                }
                else
                {
                    var currentCells = RaidHotCells.ToList();
                    for (var x = 0; x < Columns; x++)
                    {
                        for (var y = 0; y < Rows; y++)
                        {
                            var currentCell = currentCells.First(c => c.Column == x && c.Row == y);
                            var detectedAtCell = CurrentNames.FirstOrDefault(n => n.Column == x && n.Row == y);

                            if (detectedAtCell == null) // there is no text detected here
                            {
                                if (string.IsNullOrEmpty(currentCell
                                        .Name)) //both cells are still empty can safely move on
                                    continue;
                                // the detected cell is empty, but there was a name here previously. Check to see if the name moved somewhere else.
                                MoveCells(currentCell, currentCells);
                            }
                            else // there is text detected here
                            {
                                if (AreNamesCloseEnough(detectedAtCell.Name.ToLower(), currentCell.Name.ToLower(), 2)) // both cells still have the same name, safely move on
                                    continue;

                                //name doesn't match. Check for move
                                var cellWithName = currentCells.MinBy(s =>
                                    LevenshteinDistance.Compute(s.Name.ToLower(), detectedAtCell.Name.ToLower()));
                                if (LevenshteinDistance.Compute(cellWithName.Name.ToLower(),
                                        detectedAtCell.Name.ToLower()) <=
                                    2)
                                {
                                    MoveCells(currentCell, currentCells);
                                    MoveCells(cellWithName, currentCells);
                                }
                                else
                                    currentCell.Name = detectedAtCell.Name.ToUpper();
                            }
                        }

                    }

                    for (var x = 0; x < Columns; x++)
                    {
                        for (var y = 0; y < Rows; y++)
                        {
                            var currentCell = currentCells.First(c => c.Column == x && c.Row == y);
                            var detectedAtCell = CurrentNames.FirstOrDefault(n => n.Column == x && n.Row == y);
                            if (detectedAtCell == null)
                            {
                                currentCell.Reset();
                            }
                        }
                    }

                    var activeHots = CombatLogStateBuilder.CurrentState.GetCurrentlyActiveRaidHOTS(DateTime.Now);
                    var playersWithHotTimer = TimerController.GetActiveTimers().Where(t => t.SourceTimer.IsHot && t.TargetAddendem != null).Select(t => t.TargetAddendem);
                    var playersWihtActiveHot = activeHots.Select(h => h.Target.Name).Distinct().ToList();
                    foreach (var player in playersWithHotTimer)
                    {
                        var playerCell = GetCellThatMatchesName(player);
                        if (playerCell != null && !playerCell.HasHOT)
                        {
                            var timer = TimerController.GetActiveTimers().First(t => t.TargetAddendem == player);
                            if (timer.TimerValue > 0)
                                playerCell.AddHOT(timer);
                            else
                                timer.Complete(false);
                        }
                    }
                    foreach (var player in playersWihtActiveHot)
                    {
                        var playerCell = GetCellThatMatchesName(player);
                        var hotInQuestion = activeHots.First(h => h.Target.Name == player);
                        if (playerCell != null && !playerCell.HasHOT && !playersWithHotTimer.Any(p => p == player) && hotInQuestion.Source == CombatLogStateBuilder.CurrentState.LocalPlayer)
                        {
                            TimerController.TryTriggerTimer(hotInQuestion);
                        }
                    }
                    RaidHotCells = currentCells.OrderBy(c => c.Row * Columns + c.Column).ToList();
                    OnPropertyChanged("RaidHotCells");
                    OnPropertyChanged("LeftColumnCells");
                    OnPropertyChanged("RightColumnCells");
                }
            }
        }

        private RaidHotCell MoveCells(RaidHotCell currentCell, List<RaidHotCell> currentCells)
        {
            var movedName = CurrentNames.MinBy(s =>
                LevenshteinDistance.Compute(s.Name.ToLower(), currentCell.Name.ToLower()));
            if (LevenshteinDistance.Compute(movedName.Name.ToLower(), currentCell.Name.ToLower()) <=
                2) // if the closest name is close enough to be matching. Move it.
            {
                //need to take the cell in the grid that is going to receive the newly moved name and move it to this newly empty spot and clear the name.
                var source = currentCells.First(c => c.Column == movedName.Column && c.Row == movedName.Row);
                source.Column = currentCell.Column;
                source.Row = currentCell.Row;

                //move the cell with the name and data to the new location that has been cleared and moved to make room for it
                currentCell.Column = movedName.Column;
                currentCell.Row = movedName.Row;

                return source;
            }

            return null;
        }

        private void InitRaidCells()
        {
            RaidHotCells.Clear();
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {

                    RaidHotCells.Add(new RaidHotCell(Columns) { Column = c, Row = r, Name = "" });

                }
            }
        }
        public List<RaidHotCell> LeftColumnCells => RaidHotCells.Where(c=>c.Column== 0).ToList();
        public List<RaidHotCell> RightColumnCells => RaidHotCells.Where(c => c.Column == Columns-1).ToList();
        public List<RaidHotCell> RaidHotCells { get; set; } = new List<RaidHotCell>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void UpdatePositionAndSize(int actualHeight, int actualWidth, double screenHeight, double screenWidth, System.Drawing.Point topLeft)
        {
            TopLeft = topLeft;
            Height = actualHeight;
            Width = actualWidth;
            ScreenHeight = screenHeight;
            ScreenWidth = screenWidth;
            OnPropertyChanged("RowHeight");
            OnPropertyChanged("ColumnWidth");
            SizeSet = true;
        }

        internal void FirePlayerChanged(string name)
        {
            SizeSet = false;
            PlayerChanged(name);

        }
    }
}
