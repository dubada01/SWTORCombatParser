using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SWTORCombatParser.Utilities;


namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for ParselySettings.xaml
    /// </summary>
    public partial class ParselySettings : Window
    {
        private bool _isDragging;
        private Point _startPoint;

        public ParselySettings()
        {
            InitializeComponent();
            if (Settings.HasSetting("username"))
            {
                UserNameBox.Text = Settings.ReadSettingOfType<string>("username").Trim('"');
                PasswordBox.Text = Crypto.DecryptStringAES(Settings.ReadSettingOfType<string>("password").Trim('"'), "parselyInfo");
                GuildNameBox.Text = Settings.ReadSettingOfType<string>("guild").Trim('"');
            }
            SaveButton.Click += OnSave;
            CancelButton.Click += OnCancel;
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var encryptedPassword = Crypto.EncryptStringAES(PasswordBox.Text, "parselyInfo");
            var username = UserNameBox.Text;
            var guild = GuildNameBox.Text;
            Settings.WriteSetting("username", username);
            Settings.WriteSetting("password", encryptedPassword);
            Settings.WriteSetting("guild", guild);

            Close();
        }
        public void StartDragWindow(object sender, PointerPressedEventArgs args)
        {
            _isDragging = true;
            _startPoint = args.GetPosition(this);
        }
        public void DragWindow(object sender, PointerEventArgs args)
        {
            if (_isDragging)
            {
                // Get the current scaling factor to adjust the movement correctly
                var scalingFactor = this.VisualRoot.RenderScaling;

                var currentPosition = args.GetPosition(this);
                var delta = (currentPosition - _startPoint) / scalingFactor;  // Adjust for DPI scaling

                // Move the window (or element) by the delta
                var currentPositionInScreen = this.Position;
                this.Position = new PixelPoint(
                    currentPositionInScreen.X + (int)delta.X,
                    currentPositionInScreen.Y + (int)delta.Y
                );
            }
        }
        public void StopDragWindow(object sender, PointerReleasedEventArgs args)
        {
            _isDragging = false;
        }
    }
}
