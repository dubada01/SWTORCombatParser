using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels;

public class BaseOverlayViewModel:ReactiveObject
{
    private readonly BaseOverlayWindow _overlayWindow;
    internal bool _active;
    private bool _overlaysMoveable;
    private string _currentRole = "Default";
    private bool _shouldBeVisible;
    private bool _isVisibile;
    public event Action CloseRequested = delegate { };
    public event Action<Point,Point> OnNewPositionAndSize = delegate { }; 
    public event Action<bool> OnLocking = delegate { };
    public OverlaySettingsType SettingsType { get; set; } = OverlaySettingsType.Global;
    internal readonly string _overlayName;
    public object MainContent { get; set; }

    public bool ShouldBeVisible
    {
        get => _shouldBeVisible;
        set
        {
            _shouldBeVisible = value; 
            if(ShouldBeVisible && !_isVisibile && Active)
            {
                ShowOverlayWindow();
            }
            if(!_shouldBeVisible && _isVisibile)
            {
                HideOverlayWindow();
            }
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
        _overlayWindow = new BaseOverlayWindow(this);
        InitPositionAndSize();
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
            if (!_active)
            {
                HideOverlayWindow();
            }
            else
            {
                if (ShouldBeVisible || OverlaysMoveable)
                {
                    ShowOverlayWindow();
                }
            }

        }
    }
    public void ShowOverlayWindow()
    {
        if (!Active)
            return;
        Dispatcher.UIThread.Invoke(() =>
        {
            _overlayWindow?.Show();
            _isVisibile = true;
        });
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
            var thisDefault = allDefaults[_overlayName];
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