using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels;

public class BaseOverlayViewModel:ReactiveObject, INotifyPropertyChanged
{
    internal BaseOverlayWindow _overlayWindow;
    internal bool _active;
    public event Action CloseRequested = delegate { };

    public void RequestClose()
    {
        CloseRequested();
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
    public event PropertyChangedEventHandler PropertyChanged;
    public bool OverlaysMoveable { get; set; }
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
            _overlayWindow.Show();
        });
    }

    public void HideOverlayWindow()
    {
        Dispatcher.UIThread.Invoke(() => { _overlayWindow.Hide(); });
    }
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal void UpdateLock(bool value)
    {
        OverlaysMoveable = !value;
        OnPropertyChanged("OverlaysMoveable");
        OnLocking(value);
    }
}