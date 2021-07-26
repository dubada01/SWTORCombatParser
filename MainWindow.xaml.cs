using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public class CommandHandler : ICommand
    {
        private Action _action;
        private Func<bool> _canExecute;

        /// <summary>
        /// Creates instance of the command handler
        /// </summary>
        /// <param name="action">Action to be executed by the command</param>
        /// <param name="canExecute">A bolean property to containing current permissions to execute the command</param>
        public CommandHandler(Action action, Func<bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        /// <summary>
        /// Wires CanExecuteChanged event 
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Forcess checking if execute is allowed
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute.Invoke();
        }

        public void Execute(object parameter)
        {
            _action();
        }
    }
    public class PastCombat:INotifyPropertyChanged
    {
        public event Action<PastCombat> PastCombatSelected =  delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Combat Combat;
        public string CombatLabel { get; set; }
        public ICommand SelectCombatCommand => new CommandHandler(SelectCombat, ()=>true);
        public string SelectedCheck { get; set; } = " ";
        public void Reset()
        {
            SelectedCheck = " ";
            OnPropertyChanged("SelectedCheck");
        }
        private void SelectCombat()
        {
            PastCombatSelected(this);
            SelectedCheck = "✓";
            OnPropertyChanged("SelectedCheck");
            Trace.WriteLine("Selected " + CombatLabel);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public partial class MainWindow : Window
    {
        private string _logPath = @"C:\Users\duban\Documents\Star Wars - The Old Republic\CombatLogs";
        private string _currentLogName = "";
        private CombatLogStreamer _combatLogStreamer;
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        private ObservableCollection<PastCombat> _listOfPreviousCombats = new ObservableCollection<PastCombat>();
        public MainWindow()
        {
            
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            _combatLogStreamer.CombatStopped += CombatStopped;
            InitializeComponent();
            TestIdentifyCombat();
            VerifyUsingMostRecentLogFile();
            GridView.Plot.XLabel("Combat Duration (ms)");
            GridView.Plot.YLabel("Damage");
            
            GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2,title:"DPS");
            GridView.Plot.Style(ScottPlot.Style.Gray2);
            PastCombats.ItemsSource = _listOfPreviousCombats;

        }
        private void CombatStarted(string characterName)
        {
            Trace.WriteLine("CombatStarted");
            Dispatcher.Invoke(() =>
            {
                CharacterName.Text = characterName;
            });
            _totalLogsDuringCombat.Clear();
        }
        private void CombatStopped(List<ParsedLogEntry> obj)
        {
            if (obj.Count == 0)
                return;
            Trace.WriteLine("CombatStopped");
            _totalLogsDuringCombat.AddRange(obj);
            UpdateUI();
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            var combatUI = new PastCombat() { Combat = combatInfo, CombatLabel = combatInfo.StartTime.ToString() };
            combatUI.PastCombatSelected += SelectCombat;
            Dispatcher.Invoke(() => {
                _listOfPreviousCombats.Insert(0,combatUI);
            });
            
            _totalLogsDuringCombat.Clear();
            
        }
        private void SelectCombat(PastCombat selectedCombat)
        {
            foreach (var combat in _listOfPreviousCombats)
                combat.Reset();

            Trace.WriteLine("Selected " + selectedCombat.CombatLabel + " confirmed!");
            PlotCombat(selectedCombat.Combat);
        }
        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            Trace.WriteLine("Added " + obj.Count + " lines");
            _totalLogsDuringCombat.AddRange(obj);
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_totalLogsDuringCombat.Count == 0)
                return;
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat);
            PlotCombat(combatInfo);
        }
        private void PlotCombat(Combat combatToPlot)
        {
            var startPoint = combatToPlot.StartTime;
            Dispatcher.Invoke(() =>
            {
                GridView.Plot.Clear();
                PlotDamage(combatToPlot);
                PlotDamageTaken(combatToPlot);
                PlotCompanionDamage(combatToPlot);
                GridView.Plot.Legend();
                DurationValue.Text = combatToPlot.DurationSeconds.ToString("0.00");
                APMValue.Text = combatToPlot.APM.ToString("0.00");
                DamageValue.Text = combatToPlot.TotalDamage.ToString("0.00");
                DPSValue.Text = combatToPlot.DPS.ToString("0.00");
                MaxDamageValue.Text = combatToPlot.MaxDamage.ToString("0.00");
            });
        }
        private void PlotDamage(Combat combat)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsCharacter).ToList();
            PlotData(damageEntries, combat.StartTime, "Damage Output", System.Drawing.Color.Red);
        }
        private void PlotDamageTaken(Combat combat)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Target.IsCharacter).ToList();
            PlotData(damageEntries, combat.StartTime, "Incoming Damage", System.Drawing.Color.Blue);
        }
        private void PlotCompanionDamage(Combat combat)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsCompanion).ToList();
            PlotData(damageEntries, combat.StartTime, "Companion Damage", System.Drawing.Color.Magenta);
        }
        private void PlotData(List<ParsedLogEntry> data, DateTime startPoint, string label, System.Drawing.Color color)
        {
            if (data.Count > 0)
            {
                List<double> plotXvals = PlotMaker.GetPlotXVals(data, startPoint);
                List<double> plotYvals = PlotMaker.GetPlotYVals(data);
                List<double> plotYvalSums = PlotMaker.GetPlotYValRates(data, plotXvals);
                GridView.Plot.AddScatter(plotXvals.ToArray(), plotYvals.ToArray(), lineStyle: ScottPlot.LineStyle.None, markerShape: ScottPlot.MarkerShape.filledCircle, label: label, color: color);
                var scatter = GridView.Plot.AddScatter(plotXvals.ToArray(), plotYvalSums.ToArray(), lineStyle: ScottPlot.LineStyle.Solid, markerShape: ScottPlot.MarkerShape.none, label: label+"/s", color: color);
                scatter.YAxisIndex = 2;
            }
        }
        public void TestIdentifyCombat()
        {        
            var combats = CombatIdentifier.GetMostRecentLogsCombat();
            _currentLogName = combats.SourceLog;
            _combatLogStreamer.MonitorLog(System.IO.Path.Combine(_logPath, _currentLogName));
        }
        private void VerifyUsingMostRecentLogFile()
        {
            Task.Run(() => {
                while (true)
                {
                    var directoryInfo = new DirectoryInfo(_logPath);
                    var mostRecentLogName = directoryInfo.GetFiles().OrderByDescending(f=>f.LastWriteTime).First().Name;
                    if (mostRecentLogName != _currentLogName)
                        TestIdentifyCombat();
                    Thread.Sleep(250);
                }
            });
        }
    }
}
