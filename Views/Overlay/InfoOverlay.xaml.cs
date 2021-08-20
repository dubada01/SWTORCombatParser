using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for InfoOverlay.xaml
    /// </summary>
    public partial class InfoOverlay : Window
    {
        public InfoOverlay(OverlayInstanceViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
                new ExecutedRoutedEventHandler(delegate (object sender, ExecutedRoutedEventArgs args) { this.Close(); })));
        }
        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
        public void ButtonClicked(object sender, RoutedEventArgs args)
        {

        }
    }
}
