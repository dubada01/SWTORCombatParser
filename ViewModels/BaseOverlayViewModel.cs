using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels;

public class BaseOverlayViewModel:ReactiveObject
{
    internal BaseOverlayWindow _overlayWindow;
    internal bool _active;
    private bool _overlaysMoveable;
    private string _timerTitle = "Default Title";
    public event Action CloseRequested = delegate { };
    public OverlaySettingsType SettingsType { get; set; } = OverlaySettingsType.Global;
    public string OverlayName { get; set; }
    public object MainContent { get; set; }
    public void RequestClose()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CloseRequested();
        });

    }
    public event Action<bool> OnLocking = delegate { };

    public void SetLock(bool lockstate)
    {
        OnLocking(lockstate);
    }
    public event Action<string> OnCharacterDetected = delegate { };
    public void SetPlayer(string playerName)
    {
        OnCharacterDetected(playerName);
    }

    public string TimerTitle
    {
        get => _timerTitle;
        set => this.RaiseAndSetIfChanged(ref _timerTitle, value);
    }

    public bool OverlaysMoveable
    {
        get => _overlaysMoveable;
        set => this.RaiseAndSetIfChanged(ref _overlaysMoveable, value);
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            if (!_active)
            {
                HideOverlayWindow();
            }
            else
            {
                if (OverlaysMoveable)
                {
                    if(_overlayWindow!=null)
                        _overlayWindow.Show();
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
            if(_overlayWindow!=null)
                _overlayWindow.Show();
        });
    }

    public void HideOverlayWindow()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if(_overlayWindow!=null)
                _overlayWindow.Hide();
        });
    }
    internal void UpdateLock(bool value)
    {
        OverlaysMoveable = !value;
        OnLocking(value);
    }
}