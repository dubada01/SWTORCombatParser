using System.Windows;

namespace SWTORCombatParser.Utilities
{
    /// <summary>
    /// Interaction logic for LoadingSplash.xaml
    /// </summary>
    public partial class LoadingSplash : Window
    {
        public LoadingSplash()
        {
            InitializeComponent();
        }
        public void SetString(string value)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = value;
            });

        }
    }
}
