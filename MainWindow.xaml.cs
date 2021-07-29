using ScottPlot.Plottable;
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
    public class AbilitiesAndPlots
    {
        public Annotation AbilitiyAnnotation;
        public List<string> AbilitiyNames = new List<string>();
        public ScatterPlot Plot;
        public ScatterPlot RatePlot;
    }
    public class PastCombat:INotifyPropertyChanged
    {
        public event Action<PastCombat> PastCombatSelected =  delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Combat Combat;
        public string CombatLabel { get; set; }
        public string CombatDuration { get; set; }
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
        private Dictionary<string, AbilitiesAndPlots> _currentPlots = new Dictionary<string, AbilitiesAndPlots>();
        public MainWindow()
        {
            
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            _combatLogStreamer.CombatStopped += CombatStopped;

            InitializeComponent();

            DamageToggle.IsChecked = true;
            DamageToggle.Click += ToggleDamage;
            HealingToggle.IsChecked = true;
            HealingToggle.Click += ToggleHealing;
            EffectiveHealing.IsChecked = false;
            EffectiveHealing.Click += ToggleEffectiveHealing;
            DamageTakenToggle.IsChecked = true;
            DamageTakenToggle.Click += ToggleDamageTaken;
            HealingTaken.IsChecked = true;
            HealingTaken.Click += ToggleHealingTaken;
            EffectiveHealingTaken.IsChecked = false;
            EffectiveHealingTaken.Click += ToggleEffectiveHealingTaken;

            TestIdentifyCombat();
            VerifyUsingMostRecentLogFile();
            GridView.Plot.XLabel("Combat Duration (s)");
            GridView.Plot.YLabel("Ammount");
            
            GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2,title:"Rate");
            GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 3, title: "Estimated HP");
            GridView.Plot.Style(ScottPlot.Style.Gray2);

            PastCombats.ItemsSource = _listOfPreviousCombats;

        }

        private void ToggleDamageTaken(object sender, RoutedEventArgs e)
        {
            
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Incoming Damage"));
            series.Value.RatePlot.IsVisible = DamageTakenToggle.IsChecked.Value;
            series.Value.Plot.IsVisible = DamageTakenToggle.IsChecked.Value;
            GridView.Render();
        }
        private void ToggleEffectiveHealing(object sender, RoutedEventArgs e)
        {
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Outgoing Effective Healing"));
            series.Value.RatePlot.IsVisible = EffectiveHealing.IsChecked.Value;
            series.Value.Plot.IsVisible = EffectiveHealing.IsChecked.Value;
            GridView.Render();
        }
        private void ToggleHealing(object sender, RoutedEventArgs e)
        {
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Outgoing Healing"));
            series.Value.RatePlot.IsVisible = HealingToggle.IsChecked.Value;
            series.Value.Plot.IsVisible = HealingToggle.IsChecked.Value;
            GridView.Render();
        }
        private void ToggleHealingTaken(object sender, RoutedEventArgs e)
        {
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Incoming Healing"));
            series.Value.RatePlot.IsVisible = HealingTaken.IsChecked.Value;
            series.Value.Plot.IsVisible = HealingTaken.IsChecked.Value;
            GridView.Render();
        }
        private void ToggleEffectiveHealingTaken(object sender, RoutedEventArgs e)
        {
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Incoming Effective Healing"));
            series.Value.RatePlot.IsVisible = EffectiveHealingTaken.IsChecked.Value;
            series.Value.Plot.IsVisible = EffectiveHealingTaken.IsChecked.Value;
            GridView.Render();
        }
        private void ToggleDamage(object sender, RoutedEventArgs e)
        {
            var series = _currentPlots.First(kvp => kvp.Key.Contains("Outgoing Damage"));
            series.Value.RatePlot.IsVisible = DamageToggle.IsChecked.Value;
            series.Value.Plot.IsVisible = DamageToggle.IsChecked.Value;
            GridView.Render();
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
            //UpdateUI();
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            var combatUI = new PastCombat() { Combat = combatInfo, CombatLabel = string.Join(", ",combatInfo.Targets),CombatDuration = combatInfo.DurationSeconds.ToString() };
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
            var healsByAbility = selectedCombat.Combat.GetOutgoingHealingByAbility();
            var coutOfHeals = CombatMetaDataParse.Getcount(healsByAbility);
            var sumOfHeals = CombatMetaDataParse.GetSum(healsByAbility);
            var sumOfEffectiveHeals = CombatMetaDataParse.GetSum(healsByAbility,true);
            PlotCombat(selectedCombat.Combat);
        }
        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            //Trace.WriteLine("Added " + obj.Count + " lines");
            //_totalLogsDuringCombat.AddRange(obj);
            //UpdateUI();
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
                _currentPlots.Clear();
                GridView.Plot.Clear();

                var damageAnnotation = GridView.Plot.PlotAnnotation("test", 0, 0, fontSize: 20, lineColor: System.Drawing.Color.MediumVioletRed, fillColor: System.Drawing.Color.LightGray, fontColor: System.Drawing.Color.WhiteSmoke);
                damageAnnotation.IsVisible = false;
                var healingAnnotation = GridView.Plot.PlotAnnotation("test", 0, 0, fontSize: 20, lineColor: System.Drawing.Color.LightGreen, fillColor: System.Drawing.Color.LightGray, fontColor: System.Drawing.Color.WhiteSmoke);
                healingAnnotation.IsVisible = false;
                var healingTakenAnnotation = GridView.Plot.PlotAnnotation("test", 0, 0, fontSize: 20, lineColor: System.Drawing.Color.LightSeaGreen, fillColor: System.Drawing.Color.LightGray, fontColor: System.Drawing.Color.WhiteSmoke);
                healingTakenAnnotation.IsVisible = false;
                var damageTakenAnnotation = GridView.Plot.PlotAnnotation("test", 0, 0, fontSize: 20, lineColor: System.Drawing.Color.CornflowerBlue, fillColor: System.Drawing.Color.LightGray, fontColor: System.Drawing.Color.WhiteSmoke);
                damageTakenAnnotation.IsVisible = false;

                PlotDamage(combatToPlot, damageAnnotation, DamageToggle);
                PlotDamageTaken(combatToPlot, damageTakenAnnotation, DamageTakenToggle);
                //PlotCompanionDamage(combatToPlot, new Annotation());
                PlotIncomingHeals(combatToPlot,healingAnnotation);
                PlotOutgoingHeals(combatToPlot, healingTakenAnnotation);
                
                

                PlotEstimatedHP(combatToPlot);
                 GridView.Plot.SetAxisLimits(yMin:0,yAxisIndex:3);
                GridView.Plot.Legend();


                APMValue.Text = combatToPlot.APM.ToString("#,##0.00");
                DamageValue.Text = combatToPlot.TotalDamage.ToString("#,##0");
                DPSValue.Text = combatToPlot.DPS.ToString("#,##0.00");
                MaxDamageValue.Text = combatToPlot.MaxDamage.ToString("#,##0");

                HealingValue.Text = combatToPlot.TotalHealing.ToString("#,##0");
                HPSValue.Text = combatToPlot.HPS.ToString("#,##0.00");
                MaxHealingValue.Text = combatToPlot.MaxHeal.ToString("#,##0");
                EffectiveHealingValue.Text = combatToPlot.TotalEffectiveHealing.ToString("#,##0");
                EHPSValue.Text = combatToPlot.EHPS.ToString("#,##0.00");
                MaxEffectiveHealingValue.Text = combatToPlot.MaxEffectiveHeal.ToString("#,##0");
            });
        }
        private void PlotIncomingHeals(Combat combat, Annotation annotation)
        {
            PlotEffectiveHealingTaken(combat, annotation, EffectiveHealingTaken);
            PlotHealingTaken(combat, annotation, HealingTaken);
        }
        private void PlotOutgoingHeals(Combat combat, Annotation annotation)
        {
            PlotEffectiveHealing(combat, annotation, EffectiveHealing);
            PlotTotalHealing(combat, annotation, HealingToggle);
        }
        private void PlotDamage(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsPlayer).ToList();
            PlotData(damageEntries, combat.StartTime, "Outgoing Damage", System.Drawing.Color.MediumVioletRed, annotation, boxToCheck);
        }
        private void PlotDamageTaken(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Target.IsPlayer).ToList();
            PlotData(damageEntries, combat.StartTime, "Incoming Damage", System.Drawing.Color.CornflowerBlue, annotation, boxToCheck);
        }
        private void PlotTotalHealing(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Heal" && l.Source.IsPlayer).ToList();
            PlotData(damageEntries, combat.StartTime, "Outgoing Healing", System.Drawing.Color.LightGreen, annotation, boxToCheck);
        }
        private void PlotEffectiveHealing(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Heal" && l.Source.IsPlayer && l.Threat!=0).ToList();
            PlotData(damageEntries, combat.StartTime, "Outgoing Effective Healing", System.Drawing.Color.Green, annotation, boxToCheck,true,ScottPlot.MarkerShape.openCircle,9);
        }
        private void PlotHealingTaken(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Heal" && l.Target.IsPlayer).ToList();
            PlotData(damageEntries, combat.StartTime, "Incoming Healing", System.Drawing.Color.LightSeaGreen, annotation, boxToCheck);
        }
        private void PlotEffectiveHealingTaken(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Heal" && l.Target.IsPlayer && l.Threat != 0).ToList();
            PlotData(damageEntries, combat.StartTime, "Incoming Effective Healing", System.Drawing.Color.SeaGreen, annotation, boxToCheck,true, ScottPlot.MarkerShape.openCircle,9);
        }
        private void PlotCompanionDamage(Combat combat, Annotation annotation, CheckBox boxToCheck)
        {
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsCompanion).ToList();
            PlotData(damageEntries, combat.StartTime, "Companion Damage", System.Drawing.Color.Magenta, annotation, boxToCheck);
        }
        private void PlotEstimatedHP(Combat combat)
        {
            double currentHP = 300000;
            var damageEntries = combat.Logs.Where(l => l.Effect.EffectName == "Damage" && l.Target.IsPlayer).ToList();
            var healsTaken = combat.Logs.Where(l => l.Effect.EffectName == "Heal" && l.Target.IsPlayer).ToList();
            var damageTimeStamps = damageEntries.Select(d => d.TimeStamp);
            var healsTimeStamps = healsTaken.Select(h => h.TimeStamp);
            var totalTimeStamps = new List<DateTime>();
            totalTimeStamps.AddRange(healsTimeStamps);
            totalTimeStamps.AddRange(damageTimeStamps);
            var orderedTimeStamps = totalTimeStamps.OrderBy(t=>t).ToList();

            var hpOverTime = new List<double>();
            foreach(var timeStamp in orderedTimeStamps)
            {
                var tryGetDamage = damageEntries.FirstOrDefault(d => d.TimeStamp == timeStamp);
                if (tryGetDamage != null)
                {
                    var modifierValue = ((tryGetDamage.Value.Modifier?.DblValue) ?? 0);
                    var resultingDamage = tryGetDamage.Value.DblValue - modifierValue;
                    currentHP -= resultingDamage;
                }
                var trygetHealing = healsTaken.FirstOrDefault(d => d.TimeStamp == timeStamp);
                if (trygetHealing != null)
                    currentHP += trygetHealing.Threat * 2;
                hpOverTime.Add(currentHP);
            }
            var timeValues = orderedTimeStamps.Select(v => (v-combat.StartTime).TotalSeconds).ToArray();
            var values = GridView.Plot.AddScatter(timeValues, hpOverTime.ToArray(), lineStyle: ScottPlot.LineStyle.Dash, markerShape: ScottPlot.MarkerShape.none, label: "HP", color: System.Drawing.Color.Magenta);
            values.YAxisIndex = 3;

        }
        private void PlotData(List<ParsedLogEntry> data, DateTime startPoint, string label, System.Drawing.Color color, Annotation annotation, CheckBox boxToCheck,bool checkEffective = false, ScottPlot.MarkerShape marker = ScottPlot.MarkerShape.filledCircle, float size = 5)
        {
            if (data.Count > 0)
            {
                List<double> plotXvals = PlotMaker.GetPlotXVals(data, startPoint);
                List<double> plotYvals = PlotMaker.GetPlotYVals(data,checkEffective);
                List<double> plotYvalSums = PlotMaker.GetPlotYValRates(data, plotXvals,checkEffective);
                List<string> abilityNames = PlotMaker.GetAbilitityNames(data);
                var values = GridView.Plot.AddScatter(plotXvals.ToArray(), plotYvals.ToArray(), lineStyle: ScottPlot.LineStyle.None, markerShape: marker, label: label, color: color, markerSize:size);
                var rates = GridView.Plot.AddScatter(plotXvals.ToArray(), plotYvalSums.ToArray(), lineStyle: ScottPlot.LineStyle.Solid, markerShape: ScottPlot.MarkerShape.none, label: label+"/s", color: color);
                if(!boxToCheck.IsChecked.Value)
                {
                    values.IsVisible = false;
                    rates.IsVisible = false;
                }
                rates.YAxisIndex = 2;
                _currentPlots[label] = new AbilitiesAndPlots() { Plot = values, RatePlot = rates, AbilitiyNames = abilityNames, AbilitiyAnnotation = annotation};
            }
        }
        public void TestIdentifyCombat()
        {
            var combats = CombatIdentifier.GetMostRecentLogsCombat();
            //var combats = CombatIdentifier.GetSpecificCombats(System.IO.Path.Join(_logPath,"combat_2021-07-11_19_01_39_463431.txt"));
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
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            foreach (var plot in _currentPlots.Keys)
            {
                var currentPlot = _currentPlots[plot];
                (double mouseCoordX, double mouseCoordY) = GridView.GetMouseCoordinates();
                double xyRatio = GridView.Plot.XAxis.Dims.PxPerUnit / GridView.Plot.YAxis.Dims.PxPerUnit;
                (double pointX, double pointY, int pointIndex) = currentPlot.Plot.GetPointNearest(mouseCoordX, mouseCoordY, xyRatio);
                var abilities = currentPlot.AbilitiyNames;
                var annotation = currentPlot.AbilitiyAnnotation;
                annotation.X = (pointX * GridView.Plot.XAxis.Dims.PxPerUnit) - (GridView.Plot.GetAxisLimits().XMin * GridView.Plot.XAxis.Dims.PxPerUnit) + ((GridView.Plot.GetAxisLimits().XSpan * GridView.Plot.XAxis.Dims.PxPerUnit) / 75);
                annotation.Y = (GridView.Plot.GetAxisLimits().YMax * GridView.Plot.YAxis.Dims.PxPerUnit) - (pointY * GridView.Plot.YAxis.Dims.PxPerUnit) - ((GridView.Plot.GetAxisLimits().YSpan * GridView.Plot.YAxis.Dims.PxPerUnit) / 75);

                annotation.IsVisible = currentPlot.Plot.IsVisible;
                annotation.Label = abilities[pointIndex];

                if (!pointSelected.ContainsKey(plot))
                    pointSelected[plot] = pointIndex;
                Dispatcher.Invoke(() =>
                {
                    if (pointSelected[plot] != pointIndex)
                    {
                        pointSelected[plot] = pointIndex;
                        GridView.Render();
                    }
                });
            }
        }
    }
}
