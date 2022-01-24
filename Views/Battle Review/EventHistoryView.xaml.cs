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
            DataContext = _eventViewModel;
            InitializeComponent();
        }
    }
}
