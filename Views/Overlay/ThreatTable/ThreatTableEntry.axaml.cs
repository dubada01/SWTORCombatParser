using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SWTORCombatParser.ViewModels.Overlays.ThreatTable;

namespace SWTORCombatParser.Views.Overlay.ThreatTable;

public partial class ThreatTableEntry : UserControl
{
    public ThreatTableEntry(ThreatTableEntryViewModel entry)
    {
        DataContext = entry;
        InitializeComponent();
    }
}