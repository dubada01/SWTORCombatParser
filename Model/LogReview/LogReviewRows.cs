using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Collections;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Model.LogReview;

/// <summary>DataGrid can index a full result set without constructing every row or loading its icons.</summary>
public sealed class LogReviewRows : IList, IReadOnlyList<DisplayableLogEntry>, IDataGridCollectionView
{
    private ParsedLogEntry[] _logs;
    private int _count;
    private readonly Func<ParsedLogEntry, DisplayableLogEntry> _getRow;
    public LogReviewRows(ParsedLogEntry[] logs, Func<ParsedLogEntry, DisplayableLogEntry> getRow) { _logs = logs; _count = logs.Length; _getRow = getRow; }
    public int Count => _count;
    public DisplayableLogEntry this[int index] => _getRow(_logs[index]);
    object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
    public int IndexOf(object? value) => value is DisplayableLogEntry row ? Array.IndexOf(_logs, row.LogEntry) : -1;
    public bool Contains(object? value) => IndexOf(value) >= 0;
    public IEnumerator<DisplayableLogEntry> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void CopyTo(Array array, int index) { for (var i = 0; i < Count; i++) array.SetValue(this[i], index + i); }
    public bool IsReadOnly => true;
    public bool IsFixedSize => true;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    public void Append(ParsedLogEntry[] logs)
    {
        var oldCount = Count;
        _logs = logs;
        for (var i = oldCount; i < logs.Length; i++)
        {
            _count = i + 1;
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, this[i], i));
        }
    }

    // Supplying the read-only collection view prevents DataGrid from copying and
    // eagerly enumerating the entire lazy list into its default collection view.
    public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;
    public IEnumerable SourceCollection => this;
    public Func<object, bool>? Filter { get => null; set { if (value != null) throw new NotSupportedException(); } }
    public bool CanFilter => false;
    public bool CanSort => false;
    public bool CanGroup => false;
    public bool IsGrouping => false;
    public int GroupingDepth => 0;
    public string GetGroupingPropertyNameAtDepth(int depth) => throw new ArgumentOutOfRangeException(nameof(depth));
    public IAvaloniaReadOnlyList<object>? Groups => null;
    public DataGridSortDescriptionCollection SortDescriptions { get; } = new();
    public bool IsEmpty => Count == 0;
    public int CurrentPosition { get; private set; } = -1;
    public object? CurrentItem => CurrentPosition >= 0 && CurrentPosition < Count ? this[CurrentPosition] : null;
    public bool IsCurrentAfterLast => Count == 0 || CurrentPosition >= Count;
    public bool IsCurrentBeforeFirst => Count == 0 || CurrentPosition < 0;
    public event EventHandler<DataGridCurrentChangingEventArgs>? CurrentChanging;
    public event EventHandler? CurrentChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public void Refresh() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    public IDisposable DeferRefresh() => System.Reactive.Disposables.Disposable.Empty;
    public bool MoveCurrentToFirst() => MoveCurrentToPosition(0);
    public bool MoveCurrentToLast() => MoveCurrentToPosition(Count - 1);
    public bool MoveCurrentToNext() => MoveCurrentToPosition(Math.Min(Count, CurrentPosition + 1));
    public bool MoveCurrentToPrevious() => MoveCurrentToPosition(Math.Max(-1, CurrentPosition - 1));
    public bool MoveCurrentTo(object item) => MoveCurrentToPosition(IndexOf(item));
    public bool MoveCurrentToPosition(int position)
    {
        if (position < -1 || position > Count) throw new ArgumentOutOfRangeException(nameof(position));
        if (position != CurrentPosition)
        {
            var changing = new DataGridCurrentChangingEventArgs(true);
            CurrentChanging?.Invoke(this, changing);
            if (changing.Cancel) return CurrentItem != null;
            CurrentPosition = position;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
        }
        return CurrentItem != null;
    }
}
