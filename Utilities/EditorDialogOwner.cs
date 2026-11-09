using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace SWTORCombatParser.Utilities;

public static class EditorDialogOwner
{
    public static Window For(object viewModel)
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return desktop?.Windows.FirstOrDefault(window => window.IsVisible && ReferenceEquals(window.DataContext, viewModel))
            ?? desktop?.MainWindow ?? throw new InvalidOperationException("An editor requires an open owner window.");
    }
}
