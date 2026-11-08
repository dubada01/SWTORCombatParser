using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SWTORCombatParser.ViewModels.LogBrowser;

namespace SWTORCombatParser.Views.LogBrowser;

/// <summary>Returns the selected file and optional debug replay range; parsing remains with the caller.</summary>
public partial class LogBrowserWindow : Window
{
    private readonly LogBrowserViewModel _viewModel;

    public LogBrowserWindow(string directory)
    {
        _viewModel = new LogBrowserViewModel(directory);
        DataContext = _viewModel;
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e) => await _viewModel.InitializeAsync();

    private void OnClosed(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;
        _viewModel.Dispose();
    }

    private void OpenSelected_Click(object? sender, RoutedEventArgs e) => OpenSelected();
    private void Results_DoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void OpenSelected()
    {
        var path = _viewModel.SelectedLog?.FilePath;
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path))
        {
            _viewModel.ReportOpenError("This file has moved or been deleted. Refresh the library to update the results.");
            return;
        }
        Close(new LogBrowserSelection(path));
    }

    private void ReplaySelected_Click(object? sender, RoutedEventArgs e)
    {
        var request = _viewModel.CreateReplayRequest();
        if (request == null) return;
        if (!File.Exists(request.FilePath))
        {
            _viewModel.ReportOpenError("This file has moved or been deleted. Refresh the library to update the results.");
            return;
        }
        Close(new LogBrowserSelection(request.FilePath, request));
    }

    private async void BrowseFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open combat log",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Combat logs") { Patterns = new[] { "*.txt" } }, FilePickerFileTypes.All }
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) Close(new LogBrowserSelection(path));
        }
        catch (Exception ex) { _viewModel.ReportOpenError("Could not open the file browser: " + ex.Message); }
    }
}
