using SWTORCombatParser.Model.Alerts;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Overlay;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Alerts
{
    public enum AlertTypes
    {
        OutrangedHealer
    }
    public class AlertTypeOption
    {
        private bool selected;

        public event Action<AlertTypes,bool> OnSelectionChanged = delegate { };
        public AlertTypes Type { get; set; }
        public bool Selected
        {
            get => selected; set
            {
                selected = value;
                OnSelectionChanged(Type,selected);
            }
        }
    }
    public class AlertInstanceViewModel : INotifyPropertyChanged
    {
        private string alertText;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string AlertText
        {
            get => alertText; set
            {
                alertText = value;
                OnPropertyChanged();
            }
        }
    }
    public class AlertsViewModel : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private Dictionary<AlertTypes, IDisposable> _alertSubscriptions = new Dictionary<AlertTypes, IDisposable>();
        private DateTime _timeAlertUpdated;
        public AlertsViewModel()
        {
            AvailableAlertTypes.ForEach(at => at.OnSelectionChanged += ConfigureSelectedAlert);
            //AlertDisplayWindow = CreateAlertWindow();
            AlertDisplayViewModel = new AlertInstanceViewModel();
            //AlertDisplayWindow.DataContext = AlertDisplayViewModel;
            MainWindowClosing.Closing += CloseAlertWindow;
        }

        private void CloseAlertWindow()
        {
            App.Current.Dispatcher.Invoke(() => {
                //AlertDisplayWindow.Close();
            });
        }

        public List<AlertTypeOption> AvailableAlertTypes { get; set; } = Enum.GetValues(typeof(AlertTypes)).Cast<AlertTypes>().Select(t => new AlertTypeOption { Type = t }).ToList();
        public AlertWindow AlertDisplayWindow { get; set; }
        public AlertInstanceViewModel AlertDisplayViewModel { get; set; }
        private void ConfigureSelectedAlert(AlertTypes type, bool obj)
        {
            if (obj)
            {
                if (type == AlertTypes.OutrangedHealer)
                {
                    var observable = Observable.FromEvent<(Entity, List<Entity>)>(
                          handler => OutrangedHealerAlert.NotifyOutrangedHealers += handler,
                          handler => OutrangedHealerAlert.NotifyOutrangedHealers -= handler
                      ).Subscribe(fired => { HandleAlert(fired); });
                    _alertSubscriptions[type] = observable;
                }
            }
            else
            {
                _alertSubscriptions[type].Dispose();
                _alertSubscriptions.Remove(type);
            }
        }

        private bool removingAlerts = false;
        private void RemoveOldAlerts()
        {
            removingAlerts = true;
            Task.Run(() => {
                while (_timeAlertUpdated > DateTime.Now.AddSeconds(-3))
                {
                    Thread.Sleep(100);
                }
                AlertDisplayViewModel.AlertText = "";
                removingAlerts = false;
            });
        }
        private AlertWindow CreateAlertWindow()
        {
            var alertWindow = new AlertWindow();
            var screen = ScreenHandler.GetMainScreen();
            alertWindow.Left = screen.WorkingArea.Left;
            alertWindow.Top = screen.WorkingArea.Top;
            alertWindow.Width = screen.WorkingArea.Width;
            alertWindow.Height = screen.WorkingArea.Height;
            alertWindow.WindowState = System.Windows.WindowState.Maximized;
            alertWindow.Show();
            return alertWindow;
        }
        private void HandleAlert((Entity, List<Entity>) fired)
        {
            var positions = CombatLogStateBuilder.CurrentState.CurrentCharacterPositions;
            foreach (var healer in fired.Item2)
            {
                var distance = DistanceCalculator.CalculateDistanceBetweenEntities(positions[healer], positions[fired.Item1]);
                Trace.WriteLine($"{fired.Item1.Name} has outranged {healer.Name} at {distance}m");
                App.Current.Dispatcher.Invoke(() =>
                {
                    AlertDisplayViewModel.AlertText = $"{fired.Item1.Name} has outranged {healer.Name} at {distance.ToString("0")}m" ;
                    _timeAlertUpdated = DateTime.Now;
                });
            }
            if(!removingAlerts)
                RemoveOldAlerts();
        }

        
    }
}
