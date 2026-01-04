using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;

public static class ComboBoxForceSelectHack
{
    public static void Enable(ComboBox combo, string tag = "CB")
    {
        void Log(string msg) => Debug.WriteLine($"[{tag}] {msg}");

        combo.DropDownOpened += (_, __) =>
        {
            Log($"DropDownOpened  SelectedItem={combo.SelectedItem ?? "null"}  Index={combo.SelectedIndex}");

            // Let template/popup fully materialize
            Dispatcher.UIThread.Post(() =>
            {
                var popup = FindPopupFromTemplate(combo);
                if (popup == null)
                {
                    Log("Popup not found in template children.");
                    return;
                }

                if (popup.Child is not Control popupRoot)
                {
                    Log("Popup.Child is null / not Control.");
                    return;
                }

                Log($"Popup root = {popupRoot.GetType().Name}");

                // Hook inside the popup so we see real item clicks BEFORE light-dismiss closes it
                popupRoot.RemoveHandler(InputElement.PointerPressedEvent, OnPopupPressed);
                popupRoot.AddHandler(InputElement.PointerPressedEvent, OnPopupPressed,
                    RoutingStrategies.Tunnel, handledEventsToo: true);

                void OnPopupPressed(object? s, PointerPressedEventArgs e)
                {
                    var p = e.GetPosition(popupRoot);
                    var hit = popupRoot.InputHitTest(p);

                    Log($"POP Press  handled={e.Handled}  p(popup)={p}  hit={hit?.GetType().Name ?? "null"}");

                    // Walk up to ComboBoxItem
                    var v = hit as Visual;
                    while (v != null && v is not ComboBoxItem)
                        v = v.GetVisualParent();

                    if (v is not ComboBoxItem cbi)
                    {
                        Log("POP Press: no ComboBoxItem ancestor.");
                        return;
                    }

                    Log($"POP Press: ComboBoxItem found. Data={cbi.DataContext ?? "null"}");

                    // Force selection immediately (before dismiss)
                    combo.SelectedItem = cbi.DataContext;
                    combo.IsDropDownOpen = false;

                    Log($"FORCED -> SelectedItem={combo.SelectedItem ?? "null"}  Index={combo.SelectedIndex}");
                }
            }, DispatcherPriority.Loaded);
        };

        combo.DropDownClosed += (_, __) =>
            Log($"DropDownClosed  SelectedItem={combo.SelectedItem ?? "null"}  Index={combo.SelectedIndex}");
    }

    private static Popup? FindPopupFromTemplate(ComboBox combo)
    {
        // In Avalonia, the ComboBox template typically contains a Popup (often named PART_Popup).
        // Template children is the reliable way to access it.
        return combo.GetTemplateChildren().OfType<Popup>().FirstOrDefault();
    }
}
