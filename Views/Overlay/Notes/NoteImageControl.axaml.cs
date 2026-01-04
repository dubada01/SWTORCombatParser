using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Drawing;
using Avalonia.Remote.Protocol.Input;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using Image = Avalonia.Controls.Image;
using Point = Avalonia.Point;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace SWTORCombatParser.Views.Overlay.Notes
{
    public partial class NoteImageControl : UserControl
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public event Action<NoteImageControl>? DeleteRequested;
        public event Action<NoteImageControl>? MovedOrResized;

        private Image? _imageHost;
        private Button? _deleteButton;
        private Rectangle? _resizeThumb;

        private bool _dragging = false;
        private Point _startCanvasPos;
        private Point _startPointerPosInParent;
        
        private bool _resizing;
        private Point _resizeStartPointerPosInParent;
        private Point _resizeStartSize;
        
        public NoteImageControl()
        {
            InitializeComponent();

            _imageHost = this.FindControl<Image>("ImageHost");
            _deleteButton = this.FindControl<Button>("DeleteButton");
            _resizeThumb = this.FindControl<Rectangle>("ResizeThumb");

            if (_resizeThumb != null)
                _resizeThumb.Cursor = new Cursor(StandardCursorType.BottomRightCorner);

            if (_deleteButton != null)
                _deleteButton.Click += (_, __) => DeleteRequested?.Invoke(this);

            if (_imageHost != null)
            {
                _imageHost.PointerPressed += StartDrag;
                _imageHost.PointerMoved += OnDragDelta;
                _imageHost.PointerReleased += StopDrag;
            }

            if (_resizeThumb != null)
            {
                _resizeThumb.PointerPressed += StartResize;
                _resizeThumb.PointerMoved += OnResizeMove;
                _resizeThumb.PointerReleased += StopResize;
            }
        }

        private void StopDrag(object? sender, PointerReleasedEventArgs e)
        {
            if (!_dragging) return;

            _dragging = false;
            MovedOrResized?.Invoke(this);
            e.Handled = true;
        }

        private void StartDrag(object? sender, PointerPressedEventArgs e)
        {
            if (Parent is not Canvas canvas)
                return;

            _dragging = true;

            var left = Canvas.GetLeft(this);
            var top  = Canvas.GetTop(this);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top))  top  = 0;

            _startCanvasPos = new Point(left, top);
            _startPointerPosInParent = e.GetPosition(canvas);
            e.Handled = true;
        }
        private void StartResize(object? sender, PointerPressedEventArgs e)
        {
            if (Parent is not Canvas canvas)
                return;

            // left button only (optional but recommended)
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            _resizing = true;

            _resizeStartPointerPosInParent = e.GetPosition(canvas);

            // Width/Height might be NaN if not explicitly set yet
            var w = double.IsNaN(Width)  ? Bounds.Width  : Width;
            var h = double.IsNaN(Height) ? Bounds.Height : Height;

            _resizeStartSize = new Point(w, h);

            e.Handled = true;
        }

        private void OnResizeMove(object? sender, PointerEventArgs e)
        {
            if (!_resizing || Parent is not Canvas canvas)
                return;

            var current = e.GetPosition(canvas);
            var delta = current - _resizeStartPointerPosInParent;

            // bottom-right resize
            var newW = Math.Max(60, _resizeStartSize.X + delta.X);
            var newH = Math.Max(60, _resizeStartSize.Y + delta.Y);

            Width = newW;
            Height = newH;

            // if you want “live” persistence/autosave, you can raise here (or throttle)
            // MovedOrResized?.Invoke(this);

            e.Handled = true;
        }

        private void StopResize(object? sender, PointerReleasedEventArgs e)
        {
            if (!_resizing) return;

            _resizing = false;

            MovedOrResized?.Invoke(this);
            e.Handled = true;
        }
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public void SetBitmap(Bitmap bmp)
        {
            if (_imageHost != null)
                _imageHost.Source = bmp;
        }

        private void OnDragDelta(object? sender, PointerEventArgs e)
        {
            if (!_dragging || Parent is not Canvas canvas)
                return;

            var currentPointerPos = e.GetPosition(canvas);
            var delta = currentPointerPos - _startPointerPosInParent;

            Canvas.SetLeft(this, _startCanvasPos.X + delta.X);
            Canvas.SetTop(this,  _startCanvasPos.Y + delta.Y);

            e.Handled = true;
        }

        private void OnResizeDelta(object? sender, VectorEventArgs e)
        {
            Width = Math.Max(60, Width + e.Vector.X);
            Height = Math.Max(60, Height + e.Vector.Y);
        }
    }
}
