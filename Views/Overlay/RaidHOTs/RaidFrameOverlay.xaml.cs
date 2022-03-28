using SWTORCombatParser.ViewModels.Overlays.RaidHots;
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

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidFrameOverlay.xaml
    /// </summary>
    public partial class RaidFrameOverlay : Window
    {
        public RaidFrameOverlay()
        {
            InitializeComponent();
        }
        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), GetTopLeft());
        }
        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {

            Mouse.OverrideCursor = Cursors.Hand;

        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var yadjust = Height + e.VerticalChange;
            var xadjust = Width + e.HorizontalChange;
            if (xadjust > 0)
                SetValue(WidthProperty, xadjust);
            if (yadjust > 0)
                SetValue(HeightProperty, yadjust);
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(),GetWidth(), GetTopLeft());
        }
        private void Thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeNWSE;
        }
        private int GetHeight()
        {
            return (int)(ActualHeight * GetDPI().Item2);
        }
        private int GetWidth()
        {
            return (int)(ActualWidth * GetDPI().Item1);
        }
        private System.Drawing.Point GetTopLeft()
        {
            var dpi = GetDPI();
            var realTop = (int)(Top * dpi.Item2);
            var realLeft = (int)(Left * dpi.Item1);
            return new System.Drawing.Point(realLeft, realTop);
        }
        private (double,double) GetDPI()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            var dpiX = source.CompositionTarget.TransformToDevice.M11;

            var dpiY = source.CompositionTarget.TransformToDevice.M22;
            return (dpiX, dpiY);
        }
    }
}
