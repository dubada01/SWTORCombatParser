using MoreLinq;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private int rows;
        private int columns;
        private bool editable;
        private bool active;
        private RaidFrameOverlay _view;
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
                //if (active)
                //    CheckForUpdatedName();
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

        public event Action NamesUpdated = delegate { };
        public event Action<bool> ToggleLocked = delegate { };
        public event Action<string> PlayerChanged = delegate { };
        public RaidFrameOverlayViewModel(RaidFrameOverlay view)
        {
            _view = view;
            TimerNotifier.NewTimerTriggered += CheckForRaidHOT;
            //CheckForUpdatedName();
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj)
        {
            if (obj.SourceTimer.IsHot && CurrentNames.Any())
            {
                var playerName = obj.TargetAddendem.ToLower();
                var maxMatchingCharacters = CurrentNames.Select(n => n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s, playerName)).First();
                var match = LevenshteinDistance.Compute(maxMatchingCharacters, playerName);
                if (match > 3)
                    return;
                var cellToUpdate = RaidHotCells.First(c => c.Name.ToLower() == maxMatchingCharacters);
                cellToUpdate.AddTimer(obj);
            }
        }
        public void UpdateNames(List<PlacedName> orderedNames)
        {
            CurrentNames = orderedNames;
            UpdateCells();
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
        public ICommand RefreshLayoutCommand => new CommandHandler(RefreshLayout);

        private void RefreshLayout(object obj)
        {
            NamesUpdated();
        }

        private void UpdateCells()
        {
            RaidHotCells.Clear();
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {
                    var nameInPosition = CurrentNames.FirstOrDefault(n => n.Row == r && n.Column == c);

                    if (nameInPosition != null)
                    {
                        RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = nameInPosition.Name });
                    }
                    else
                    {
                        RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = "" });
                    }

                }
            }
            OnPropertyChanged("RaidHotCells");
        }
        //private void CheckForUpdatedName()
        //{
        //    Task.Run(() => {
        //        while (true)
        //        {
        //            if (!Active)
        //                break;
        //            if (CurrentNames.Any())
        //            {
        //                foreach (var name in CurrentNames)
        //                {
        //                    if (Editable)
        //                    {
        //                        Application.Current.Dispatcher.Invoke(() => {
        //                            _view.Hide();
        //                        });
        //                    }
        //                    var image = RaidFrameScreenGrab.GetRaidFrameBitmap(name.Vertices.First(), (name.Vertices[1].X - name.Vertices[0].X), (name.Vertices[2].Y - name.Vertices[0].Y));
        //                    if (Editable)
        //                    {
        //                        Application.Current.Dispatcher.Invoke(() => {
        //                            _view.Show();
        //                        });
        //                    }
        //                    if(name.PixelsAtNameLocation == null)
        //                        name.PixelsAtNameLocation = image;
        //                    if(RaidFrameScreenGrab.GetDifferenceOfAverage(name.PixelsAtNameLocation, image) > 20)
        //                    {
        //                        image.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), name.Name + "NEW pixels.png"));
        //                        name.PixelsAtNameLocation.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), name.Name + "OLD pixels.png"));
        //                        Trace.WriteLine(name.Name + " was different by " + RaidFrameScreenGrab.GetDifferenceOfAverage(name.PixelsAtNameLocation, image));
        //                        NamesUpdated();
        //                        break;
        //                    }
        //                }
        //            }
        //            Thread.Sleep(5000);
        //        }
        //    });
        //}
        public ObservableCollection<RaidHotCell> RaidHotCells { get; set; } = new ObservableCollection<RaidHotCell>();

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
        }

        internal void FirePlayerChanged(string name)
        {
            PlayerChanged(name);

        }
    }
}
