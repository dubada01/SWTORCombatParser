using ScottPlot.Plottable;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.Utilities;
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

    public class AbilitiesAndPlots
    {
        public Annotation AbilitiyAnnotation;
        public List<string> AbilitiyNames = new List<string>();
        public ScatterPlot Plot;
        public ScatterPlot RatePlot;
    }

    public partial class MainWindow : Window
    {
        private string _logPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        private string _currentLogName = "";
        private CombatLogStreamer _combatLogStreamer;
        private PlotViewModel _plotViewModel;
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        private ObservableCollection<PastCombat> _listOfPreviousCombats = new ObservableCollection<PastCombat>();

        public MainWindow()
        {
            ClassIdentifier.InitializeAvailableClasses();
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            _combatLogStreamer.CombatStopped += CombatStopped;
            
            InitializeComponent();
            _plotViewModel = new PlotViewModel(GridView, this);
            

            TestIdentifyCombat();
            //VerifyUsingMostRecentLogFile();
            GridView.Plot.XLabel("Combat Duration (s)");
            GridView.Plot.YLabel("Ammount");
            
            GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2,title:"Rate");
            //GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 3, title: "Estimated HP");
            GridView.Plot.Style(ScottPlot.Style.Gray2);
           // GridView.Plot.SetAxisLimits(yMin: 0, yAxisIndex: 3);
            GridView.Plot.Legend();

            _plotViewModel.SetUpLegend(new List<PlotType> { PlotType.DamageOutput, PlotType.DamageTaken, PlotType.HealingOutput, PlotType.HealingTaken });
            InteractiveLegend.ItemsSource = _plotViewModel.GetLegends();

            PastCombats.ItemsSource = _listOfPreviousCombats;

        }
        public void RenderPlot()
        {
            Dispatcher.Invoke(() => {
                GridView.Render();
            });
        }
        public void RenderAndResize()
        {
            Dispatcher.Invoke(() => {
                GridView.Plot.AxisAuto();
                GridView.Render();
            });
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
            //var logState = CombatLogParser.BuildLogState(CombatLogLoader.LoadSpecificLog(System.IO.Path.Join("TestCombatLogs", _currentLogName)));
            //var abilities = logState.Modifiers.Select(m => m.Name).Distinct();
            //var durations = logState.Modifiers.Where(m=>m.StartTime >= obj.First().TimeStamp && m.StopTime < obj.Last().TimeStamp).GroupBy(v => v.Name, v => v.DurationSeconds, (name, durations) => new { Name = name, SumOfDurations = durations.Sum(), CountOfAbilities = durations.Count() }).OrderByDescending(effect => effect.SumOfDurations);
            //Trace.WriteLine("------ABILITY DURATIONS------");
            //Trace.WriteLine(combatInfo.DurationSeconds);
            //durations.ToList().ForEach(a => Trace.WriteLine("Ability: " + a.Name + " Duration: " + a.SumOfDurations+ " Number of: "+a.CountOfAbilities));
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
            PopulateMetaData(selectedCombat.Combat);
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
            PopulateMetaData(combatInfo);
            PlotCombat(combatInfo);
        }
        private void PlotCombat(Combat combatToPlot)
        {
            _plotViewModel.PlotData(combatToPlot);
        }

        private void PopulateMetaData(Combat combatToPlot)
        {
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
        }

        public void TestIdentifyCombat()
        {
            // var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            var mostRecentLog = CombatLogLoader.LoadSpecificLog(System.IO.Path.Join("TestCombatLogs", "combat_2021-07-11_20_54_51_389708.txt"));
            _currentLogName = mostRecentLog.Name;
            CombatLogParser.BuildLogState(mostRecentLog);
            //_combatLogStreamer.MonitorLog(System.IO.Path.Join(_logPath, mostRecentLog.Name));
            _combatLogStreamer.MonitorLog(System.IO.Path.Join("TestCombatLogs", "combat_2021-07-11_20_54_51_389708.txt"));
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

        private DateTime _lastAnnotationUpdateTime;
        private double _annotationUpdatePeriodMS = 50;
        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            if ((DateTime.Now - _lastAnnotationUpdateTime).TotalMilliseconds > _annotationUpdatePeriodMS)
            {
                _lastAnnotationUpdateTime = DateTime.Now;
            }
            else
                return;

            _plotViewModel.MousePositionUpdated();
        }
       
    }
}
