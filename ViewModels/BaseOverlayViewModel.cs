using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using MvvmHelpers;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels;

public abstract class BaseOverlayViewModel:ReactiveObject
{
    private BaseOverlayWindow _overlayWindow;
    internal bool _active;
    private bool _overlaysMoveable;
    private string _currentRole = "Default";
    private bool _shouldBeVisible;
    private bool _isVisibile;
    public event Action<bool> ActiveChanged = delegate { };
    public event Action CloseRequested = delegate { };
    public event Action<Point,Point> OnNewPositionAndSize = delegate { }; 
    public event Action<bool> OnLocking = delegate { };
    public Point OverlayScaledSize { get; set; }
    public Point OverlayPosition { get; set; }
    public OverlaySettingsType SettingsType { get; set; } = OverlaySettingsType.Global;
    internal readonly string _overlayName;
    private UserControl _mainContent;
    private bool _displayingContent;

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

    public void UpdateVisibility()
    {
        this.RaisePropertyChanged(nameof(ShouldBeVisible));
        if (!_active || (!OverlaysMoveable && !ShouldBeVisible))
        {
            HideOverlayWindow();
        }
        else
        {
            if ((ShouldBeVisible || OverlaysMoveable) && DisplayingContent)
            {
                ShowOverlayWindow();
                if (OverlaysMoveable)
                    OnLocking(false);
            }
        }
    }
    public bool KeepBackgroundHidden { get; set; }
    public abstract bool ShouldBeVisible
    {
        get;
    }
    public bool HideUnlessDisplayingContent { get; set; }
    public bool DisplayingContent
    {
        get => _displayingContent || !HideUnlessDisplayingContent;
        set
        {
            this.RaiseAndSetIfChanged(ref _displayingContent, value);
            UpdateVisibility();
        }
    }

    public void RequestClose()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CloseRequested();
        });

    }
    public BaseOverlayViewModel(string overlayName)
    {
        _overlayName = overlayName;
    }
    // A method to explicitly create the window once the derived class has been constructed
    public void InitializeOverlayWindow()
    {
        if (_overlayWindow == null)
        {
            _overlayWindow = new BaseOverlayWindow(this);  // Pass `this`, referring to the fully constructed derived class
        }
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
        if ((ShouldBeVisible || OverlaysMoveable) && DisplayingContent)
        {
            if (!Active)
                return;
            Dispatcher.UIThread.Invoke(() =>
            {
                _overlayWindow?.Show();
                _isVisibile = true;
            });
        }
    }

    public void HideOverlayWindow()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            _overlayWindow?.Hide();
            _isVisibile = false;
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