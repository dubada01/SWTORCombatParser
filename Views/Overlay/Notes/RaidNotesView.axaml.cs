using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using ReactiveUI;
using SWTORCombatParser.Model.Notes;
using SWTORCombatParser.ViewModels.Overlays.Notes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.VisualTree;

namespace SWTORCombatParser.Views.Overlay.Notes
{
    public partial class RaidNotesView : UserControl
    {
        private TextBox? _notesTextBox;
        private Canvas? _overlayCanvas;

        private RaidNotesViewModel? _vm;
        private bool _loadingRaid;
        private Point? _lastCanvasPointerPos;
        private Point? _pressPoint;
        public RaidNotesView()
        {
            InitializeComponent();
            
            _notesTextBox = this.FindControl<TextBox>("NotesTextBox");
            _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");

            // Click-through empty space
            if (_overlayCanvas != null)
                _overlayCanvas.Background = null;

            // Track pointer position (for paste placement) WITHOUT relying on canvas hit-test
            this.PointerMoved += (_, e) =>
            {
                if (_overlayCanvas != null)
                    _lastCanvasPointerPos = e.GetPosition(_overlayCanvas);
            };

            // Ctrl+V (image paste)
            this.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);

            AttachedToVisualTree += (_, __) => HookVm();

            
        }

        public RaidNotesView(RaidNotesViewModel vm) : this() => DataContext = vm;

        private void HookVm()
        {
            _vm = DataContext as RaidNotesViewModel;
            if (_vm == null) return;

            _vm.WhenAnyValue(x => x.SelectedRaid)
               .DistinctUntilChanged()
               .ObserveOn(RxApp.MainThreadScheduler)
               .Subscribe(_ => RebuildOverlayForSelectedRaid());

            TestBox.SelectionChanged += (sender, args) => Debug.WriteLine("Selection changed");
            ComboBoxForceSelectHack.Enable(TestBox, "Raid Notes TestBox");
        }

        private void RebuildOverlayForSelectedRaid()
        {
            if (_overlayCanvas == null || _vm == null) return;

            _loadingRaid = true;
            try
            {
                _overlayCanvas.Children.Clear();

                foreach (var img in _vm.GetImagesForSelectedRaid())
                {
                    var bmp = DecodePng(img.PngBase64);
                    if (bmp == null) continue;

                    var sticker = CreateSticker(img.Id, bmp);

                    sticker.Width = img.W;
                    sticker.Height = img.H;

                    Canvas.SetLeft(sticker, img.X);
                    Canvas.SetTop(sticker, img.Y);

                    _overlayCanvas.Children.Add(sticker);
                }
            }
            finally
            {
                _loadingRaid = false;
            }
        }

        private NoteImageControl CreateSticker(Guid id, Bitmap bmp)
        {
            var sticker = new NoteImageControl
            {
                Id = id,
                Width = 240,
                Height = 180,
                IsHitTestVisible = true
            };
            sticker.SetBitmap(bmp);

            sticker.DeleteRequested += c =>
            {
                if (_overlayCanvas != null)
                    _overlayCanvas.Children.Remove(c);

                _vm?.RemoveImageForSelectedRaid(c.Id);
            };

            sticker.MovedOrResized += c =>
            {
                if (_vm == null || _overlayCanvas == null || _loadingRaid) return;

                var left = Canvas.GetLeft(c);
                var top = Canvas.GetTop(c);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                // keep existing png bytes in store
                var existing = _vm.GetImagesForSelectedRaid().FirstOrDefault(x => x.Id == c.Id);
                var png = existing?.PngBase64 ?? "";

                _vm.UpsertImageForSelectedRaid(new RaidNoteImage
                {
                    Id = c.Id,
                    PngBase64 = png,
                    X = left,
                    Y = top,
                    W = c.Width,
                    H = c.Height
                });
            };

            // Bring to front when clicked/dragged
            sticker.PointerPressed += (_, __) =>
            {
                if (_overlayCanvas == null) return;
                if (_overlayCanvas.Children.Contains(sticker))
                {
                    _overlayCanvas.Children.Remove(sticker);
                    _overlayCanvas.Children.Add(sticker);
                }
            };

            return sticker;
        }

        private async void OnKeyDownTunnel(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (await TryPasteImageAsync())
                    e.Handled = true;
            }
        }

        private async Task<bool> TryPasteImageAsync()
        {
            if (_overlayCanvas == null || _vm == null || _loadingRaid) return false;

            var top = TopLevel.GetTopLevel(this);
            var clipboard = top?.Clipboard;
            if (clipboard == null) return false;

            var formats = await clipboard.GetFormatsAsync();
            if (formats == null || formats.Length == 0) return false;

            string[] preferred = { "image/png", "image/jpeg", "image/jpg", "image/bmp", "PNG" };
            var chosen =
                preferred.FirstOrDefault(f => formats.Contains(f, StringComparer.OrdinalIgnoreCase))
                ?? formats.FirstOrDefault(f => f.Contains("image", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(chosen)) return false;

            var data = await clipboard.GetDataAsync(chosen);
            if (data == null) return false;

            Bitmap bmp;
            if (data is byte[] bytes && bytes.Length > 0)
                bmp = new Bitmap(new MemoryStream(bytes));
            else if (data is Stream s)
                bmp = new Bitmap(s);
            else
                return false;

            // create and place
            var id = Guid.NewGuid();
            var sticker = CreateSticker(id, bmp);

            var pos = _lastCanvasPointerPos ?? new Point(60, 60);
            Canvas.SetLeft(sticker, Math.Max(0, pos.X - 40));
            Canvas.SetTop(sticker, Math.Max(0, pos.Y - 40));

            _overlayCanvas.Children.Add(sticker);

            // persist to VM
            _vm.UpsertImageForSelectedRaid(new RaidNoteImage
            {
                Id = id,
                PngBase64 = EncodePng(bmp),
                X = Canvas.GetLeft(sticker),
                Y = Canvas.GetTop(sticker),
                W = sticker.Width,
                H = sticker.Height
            });

            return true;
        }

        private static string EncodePng(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms); // PNG
            return Convert.ToBase64String(ms.ToArray());
        }

        private static Bitmap? DecodePng(string b64)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(b64)) return null;
                var bytes = Convert.FromBase64String(b64);
                return new Bitmap(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }
    }
}
