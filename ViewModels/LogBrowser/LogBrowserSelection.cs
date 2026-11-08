using System;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.ViewModels.LogBrowser;

public sealed record LogBrowserSelection(string FilePath, LogReplayRequest? Replay = null);
public sealed record LogReplayChoice(string Label, DateTime? From = null, DateTime? Through = null);
