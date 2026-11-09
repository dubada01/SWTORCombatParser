using ReactiveUI;
using SWTORCombatParser.Model.Overlays;

namespace SWTORCombatParser.ViewModels.Overlays;

public sealed class OverlayPresentationOption(string key, string name) : ReactiveObject
{
    public string Key { get; } = key;
    public string Name { get; } = name;
    public string[] Styles => OverlayPresentationSettings.Styles;
    public int StyleIndex
    {
        get => OverlayPresentationSettings.Read(Key);
        set => OverlayPresentationSettings.Write(Key, value);
    }
    public void Refresh() => this.RaisePropertyChanged(nameof(StyleIndex));
    public override string ToString() => Name;
}
