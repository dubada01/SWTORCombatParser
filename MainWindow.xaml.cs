using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public partial class MainWindow : Window
    {
        private string _logPath = @"C:\Users\duban\Documents\Star Wars - The Old Republic\CombatLogs";
        private string _currentLogName = "";
        private CombatLogStreamer _combatLogStreamer;
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        public MainWindow()
        {
            
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            _combatLogStreamer.CombatStopped += CombatStopped;
            InitializeComponent();
            TestIdentifyCombat();
            VerifyUsingMostRecentLogFile();
            GridView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2);
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
            Trace.WriteLine("CombatStopped");
            _totalLogsDuringCombat.AddRange(obj);
            UpdateUI();
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
            //var startTime = DateTime.Now;
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat);

            var startPoint = _totalLogsDuringCombat[0].TimeStamp;
            //Trace.WriteLine("Got MetaData in: " + (DateTime.Now - startTime).TotalMilliseconds);
            Dispatcher.Invoke(() =>
            {
                GridView.Plot.Clear();
                PlotDamage(startPoint);
                PlotDamageTaken(startPoint);
                PlotCompanionDamage(startPoint);
                GridView.Plot.Legend();
                DurationValue.Text = combatInfo.DurationSeconds.ToString("0.00");
                APMValue.Text = combatInfo.APM.ToString("0.00");
                DamageValue.Text = combatInfo.TotalDamage.ToString("0.00");
                DPSValue.Text = combatInfo.DPS.ToString("0.00");
                MaxDamageValue.Text = combatInfo.MaxDamage.ToString("0.00");
            });
        }

        private void PlotDamage(DateTime startPoint)
        {
            var damageEntries = _totalLogsDuringCombat.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsCharacter).ToList();
            PlotData(damageEntries, startPoint, "Damage Output", System.Drawing.Color.Red);
        }
        private void PlotDamageTaken(DateTime startPoint)
        {
            var damageEntries = _totalLogsDuringCombat.Where(l => l.Effect.EffectName == "Damage" && l.Target.IsCharacter).ToList();
            PlotData(damageEntries, startPoint, "Incoming Damage", System.Drawing.Color.Blue);
        }
        private void PlotCompanionDamage(DateTime startPoint)
        {
            var damageEntries = _totalLogsDuringCombat.Where(l => l.Effect.EffectName == "Damage" && l.Source.IsCompanion).ToList();
            PlotData(damageEntries, startPoint, "Companion Damage", System.Drawing.Color.Magenta);
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
