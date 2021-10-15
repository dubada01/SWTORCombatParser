using SWTORCombatParser.ViewModels.BattleReview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
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

namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for EventHistoryView.xaml
    /// </summary>
    public partial class EventHistoryView : UserControl
    {
        public EventHistoryView(EventHistoryViewModel _eventViewModel)
        {
            var sched = SynchronizationContext.Current;
            DataContext = _eventViewModel;
            var upddate = Observable.Interval(TimeSpan.FromSeconds(0.1)).ObserveOn(sched);
            var updated = Observable.FromEvent<double>(
                handler => ReviewSliderUpdates.OnSliderUpdated += handler,
                handler => ReviewSliderUpdates.OnSliderUpdated += handler).ObserveOn(sched);
            var joined = upddate.And(updated).Then((l, d) => d);
            Observable.When(joined).Subscribe(updated => KeepListAtBottom());
            InitializeComponent();
        }
        private void KeepListAtBottom()
        {
            if (EventsList.Items.Count == 0)
                return;
            EventsList.ScrollIntoView(EventsList.Items[EventsList.Items.Count - 1]);
        }
    }
}
