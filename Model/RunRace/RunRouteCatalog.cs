using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Orbs.RunRace;

namespace SWTORCombatParser.Model.RunRace;

public static class RunRouteCatalog
{
    private static readonly Lazy<RunRouteDefinition[]> Definitions = new(() =>
        JsonSerializer.Deserialize<RunRouteDefinition[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "DataStructures", "EncounterInfo", "RunRouteCatalog.json"))) ?? Array.Empty<RunRouteDefinition>());
    public static RunRouteDefinition? Find(string encounter) => Definitions.Value.FirstOrDefault(d => d.Encounter == encounter);
}
