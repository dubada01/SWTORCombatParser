using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels;

public abstract class BaseOverlayViewModel : ReactiveObject
{
    // Fields
    public readonly string _overlayName;
    private BaseOverlayWindow _overlayWindow;
    private UserControl _mainContent;
    private bool _active;
    private bool _overlaysMoveable;
    private bool _inConversation;
    private bool _isHidden = true;
    private string _currentRole = "Default";

    // Constants
    private const double DefaultLockedOpacity = 0.066;
    private const double DefaultUnlockedOpacity = 0.75;

    // Events
    public event Action<bool> ActiveChanged = delegate { };
    public event Action CloseRequested = delegate { };
    public event Action<Point, Point> OnNewPositionAndSize = delegate { };
    public event Action<bool> OnLocking = delegate { };

    // Properties
    public Point OverlayScaledSize { get; set; }
    public Point OverlayPosition { get; set; }
    public OverlaySettingsType SettingsType { get; set; } = OverlaySettingsType.Global;

    public UserControl MainContent
    {
        get => _mainContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _mainContent, value);
            InitializeOverlayWindow();
            InitPositionAndSize();
        }
    }

    public double BackgroundLockedOpacity { get; set; } = DefaultLockedOpacity;
    public double BackgroundUnlockedOpacity { get; set; } = DefaultUnlockedOpacity;

    public bool OverlaysMoveable
    {
        get => _overlaysMoveable;
        set
        {
            this.RaiseAndSetIfChanged(ref _overlaysMoveable, value);
            UpdateVisibility();
            OnLocking(!_overlaysMoveable);
        }
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            UpdateActiveState(value);
            UpdateVisibility();
            ActiveChanged(_active);
        }
    }

    public abstract bool ShouldBeVisible { get; }

    // Constructor
    protected BaseOverlayViewModel(string overlayName)
    {
        _overlayName = overlayName;
        CombatLogStreamer.NewLineStreamed += ToggleVisibilityFromConversation;
    }

    // Methods
    public void RequestClose() => Dispatcher.UIThread.Invoke(CloseRequested);

    public void TemporarilyHide() => _active = false;

    public void InitializeOverlayWindow()
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new BaseOverlayWindow(this);
        }
    }

    public void SetAutoScaleHeight() => Dispatcher.UIThread.Invoke(() => _overlayWindow.SizeToContent = SizeToContent.Height);

    public void SetRole(string role)
    {
        _currentRole = role;
        InitPositionAndSize();
    }

    public void ShowOverlayWindow()
    {
        if ((ShouldBeVisible || OverlaysMoveable) && Active && _isHidden)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _isHidden = false;
                _overlayWindow?.Show();
                _overlayWindow.ToggleClickThroughCrossPlatform(!OverlaysMoveable);
            });
        }
    }

    public void HideOverlayWindow()
    {
        if (!_isHidden)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _overlayWindow?.Hide();
                _isHidden = true;
            });
        }
    }

    public void InitPositionAndSize()
    {
        if (SettingsType == OverlaySettingsType.Global)
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
            Active = defaults.Acive;
            OnNewPositionAndSize(defaults.Position, defaults.WidtHHeight);
        }
        else if (SettingsType == OverlaySettingsType.Character)
        {
            var allDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentRole);
            if (allDefaults.TryGetValue(_overlayName, out var thisDefault))
            {
                Active = thisDefault.Acive;
                OnNewPositionAndSize(thisDefault.Position, thisDefault.WidtHHeight);
            }
        }
    }

    public void UpdateWindowProperties(Point position, Point size)
    {
        if (SettingsType == OverlaySettingsType.Global)
        {
            DefaultGlobalOverlays.SetDefault(_overlayName, position, size);
        }
        else if (SettingsType == OverlaySettingsType.Character)
        {
            DefaultCharacterOverlays.SetCharacterDefaults(_overlayName, position, size, _currentRole);
        }
    }

    public void UpdateWindowSizeWithScale(Point position, Point size)
    {
        OverlayScaledSize = size;
        OverlayPosition = position;
    }

    public void UpdateActiveState(bool state)
    {
        if (SettingsType == OverlaySettingsType.Global)
        {
            DefaultGlobalOverlays.SetActive(_overlayName, state);
        }
        else if (SettingsType == OverlaySettingsType.Character)
        {
            DefaultCharacterOverlays.SetActiveStateCharacter(_overlayName, state, _currentRole);
        }
    }

    public void CloseButtonClicked()
    {
        Active = false;
        RequestClose();
    }

    private void ToggleVisibilityFromConversation(ParsedLogEntry obj)
    {
        if (_overlayWindow == null) return;

        if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply && obj.Source.IsLocalPlayer && !_inConversation)
        {
            _inConversation = true;
            UpdateVisibility();
        }
        else if ((obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove) ||
                 (obj.Effect.EffectType == EffectType.AreaEntered && obj.Source.IsLocalPlayer && _inConversation))
        {
            _inConversation = false;
            UpdateVisibility();
            _overlayWindow.ToggleClickThroughCrossPlatform(!OverlaysMoveable);
        }
    }

    public void UpdateVisibility()
    {
        if (_overlayWindow == null) return;

        this.RaisePropertyChanged(nameof(ShouldBeVisible));

        if (!_active || (!OverlaysMoveable && !ShouldBeVisible) || _inConversation)
        {
            HideOverlayWindow();
        }
        else
        {
            ShowOverlayWindow();
        }
    }
}