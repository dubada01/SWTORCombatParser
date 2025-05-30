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

public abstract class BaseOverlayViewModel:ReactiveObject
{
    public BaseOverlayWindow _overlayWindow;
    internal bool _active;
    private bool _overlaysMoveable;
    private string _currentRole = "Default";
    public event Action<bool> ActiveChanged = delegate { };
    public event Action CloseRequested = delegate { };
    public event Action<Point,Point> OnNewPositionAndSize = delegate { }; 
    public event Action<bool> OnLocking = delegate { };
    public Point OverlayScaledSize { get; set; }
    public Point OverlayPosition { get; set; }
    public OverlaySettingsType SettingsType { get; set; } = OverlaySettingsType.Global;
    internal readonly string _overlayName;
    private UserControl _mainContent;
    private bool _inConversation;
    private static double _defaultLockedOpacity = 0.066;
    private static double _defaultUnLockedOpacity = 0.75;
    private bool _isHidden = true;

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

    public double BackgroundLockedOpacity { get; set; } = _defaultLockedOpacity;
    public double BackgroundUnLockedOpacity { get; set; } = _defaultUnLockedOpacity;
    public void UpdateVisibility()
    {
        if (_overlayWindow == null)
            return;
        this.RaisePropertyChanged(nameof(ShouldBeVisible));
        if (!_active || (!OverlaysMoveable && !ShouldBeVisible) || _inConversation)
        {
            HideOverlayWindow();
        }
        else
        {
            if ((ShouldBeVisible || OverlaysMoveable))
            {
                ShowOverlayWindow();
            }
        }
    }
    public abstract bool ShouldBeVisible
    {
        get;
    }
    public void RequestClose()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CloseRequested();
        });
    }
    public void TemporarilyHide()
    {
        _active = false;
    }
    public BaseOverlayViewModel(string overlayName)
    {
        _overlayName = overlayName;
        CombatLogStreamer.NewLineStreamed += ToggleVisibilityFromConversation;
    }

    private void ToggleVisibilityFromConversation(ParsedLogEntry obj)
    {
        if(_overlayWindow == null)
            return;
        if (obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Apply && obj.Source.IsLocalPlayer && !_inConversation)
        {
            _inConversation = true;
            UpdateVisibility();
        }
        if ((obj.Effect.EffectId == _7_0LogParsing.InConversationEffectId && obj.Effect.EffectType == EffectType.Remove) || (obj.Effect.EffectType == EffectType.AreaEntered) && obj.Source.IsLocalPlayer && _inConversation)
        {
            _inConversation = false;
            UpdateVisibility();
            _overlayWindow.ToggleClickThroughCrossPlatform(!OverlaysMoveable);
        }
        
    }

    // A method to explicitly create the window once the derived class has been constructed
    public void InitializeOverlayWindow()
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new BaseOverlayWindow(this);  // Pass `this`, referring to the fully constructed derived class
        }
    }

    public void SetAutoScaleHeight()
    {
        Dispatcher.UIThread.Invoke(() =>
        {        
            _overlayWindow.SizeToContent = SizeToContent.Height;
        });

    }
    public void SetRole(string role)
    {
        _currentRole = role;
        InitPositionAndSize();
    }

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
    public void ShowOverlayWindow()
    {
        if ((ShouldBeVisible || OverlaysMoveable))
        {
            if (!Active || !_isHidden)
                return;
            Dispatcher.UIThread.Invoke(() =>
            {
                _isHidden = false;
                _overlayWindow?.Show();
                _overlayWindow.ToggleClickThroughCrossPlatform(!_overlaysMoveable);
            });
        }
    }

    public void HideOverlayWindow()
    {
        if(_isHidden)
            return;
        Dispatcher.UIThread.Invoke(() =>
        {
            _overlayWindow?.Hide();
            _isHidden = true;
        });
    }
    public void InitPositionAndSize()
    {
        if (SettingsType == OverlaySettingsType.Global)
        {
            var defaults = DefaultGlobalOverlays.GetOverlayInfoForType(_overlayName);
            Active = defaults.Acive;
            OnNewPositionAndSize(defaults.Position, defaults.WidtHHeight);
        }

        if (SettingsType == OverlaySettingsType.Character)
        {
            var allDefaults = DefaultCharacterOverlays.GetCharacterDefaults(_currentRole);
            if (!allDefaults.TryGetValue(_overlayName, out var thisDefault))
                return;
            Active = thisDefault.Acive;
            OnNewPositionAndSize(thisDefault.Position, thisDefault.WidtHHeight);
        }
    }
    public void UpdateWindowProperties(Point position, Point size)
    {
        if(SettingsType == OverlaySettingsType.Global)
            DefaultGlobalOverlays.SetDefault(_overlayName, position, size);
        if(SettingsType == OverlaySettingsType.Character)
            DefaultCharacterOverlays.SetCharacterDefaults(_overlayName, position, size,_currentRole);
        
    }
    public void UpdateWindowSizeWithScale(Point position, Point size)
    {
        OverlayScaledSize = size;
        OverlayPosition = position;
    }
    public void UpdateActiveState(bool state)
    {
        if(SettingsType == OverlaySettingsType.Global)
            DefaultGlobalOverlays.SetActive(_overlayName, state);
        if(SettingsType == OverlaySettingsType.Character)
            DefaultCharacterOverlays.SetActiveStateCharacter(_overlayName, state,_currentRole);
    }
    public void CloseButtonClicked()
    {
        Active = false;
        RequestClose();
    }
}