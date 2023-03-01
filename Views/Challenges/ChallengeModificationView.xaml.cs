using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Challenges;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Challenges
{
    /// <summary>
    /// Interaction logic for ChallengeModificationView.xaml
    /// </summary>
    public partial class ChallengeModificationView : Window
    {
        ChallengeModificationViewModel _vm;
        public ChallengeModificationView(ChallengeModificationViewModel vm)
        {
            InitializeComponent();
            DataContext= vm;
            _vm= vm;
            Owner = App.Current.MainWindow;
            Left = Application.Current.MainWindow.Left + (Application.Current.MainWindow.ActualWidth / 2) - (750 / 2d);
            Top = Application.Current.MainWindow.Top + (Application.Current.MainWindow.ActualHeight / 2) - (450 / 2d);
            _vm.OnNewChallenge += CloseWindow;
            CancelButton.Click += Cancel;
        }
        private void CloseWindow(Challenge throwAway, bool meh)
        {
            Close();
        }
        private void Cancel(object sender, RoutedEventArgs e)
        {
            _vm.Cancel();
            Close();
        }

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
    }
}
