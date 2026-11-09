using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SWTORCombatParser.Model.Phases
{
    public static class DefaultPhaseManager
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");

        private static string _phaseFile = Path.Combine(appDataPath, "phase_info.json");
        private static readonly object FileLock = new();

        public static IEnumerable<Phase> GetExisitingPhases()
        {
            lock (FileLock) return ReadPhases(_phaseFile);
        }
        public static void RemovePhase(Phase phase)
        {
            lock (FileLock)
            {
                var phases = ReadPhases(_phaseFile);
                phases.RemoveAll(p => p.Id == phase.Id);
                WritePhases(_phaseFile, phases);
            }
        }
        public static void AddOrUpdatePhase(Phase phase)
        {
            AddOrUpdatePhases(new[] { phase });
        }

        public static void AddOrUpdatePhases(IEnumerable<Phase> updates)
        {
            var replacements = updates.ToArray();
            lock (FileLock)
            {
                var phases = ReadPhases(_phaseFile);
                foreach (var phase in replacements)
                {
                    phases.RemoveAll(p => p.Id == phase.Id);
                    phases.Add(phase);
                }
                WritePhases(_phaseFile, phases);
            }
        }

        internal static List<Phase> ReadPhases(string path)
        {
            if (!File.Exists(path)) return new List<Phase>();

            var text = File.ReadAllText(path);
            var phases = new List<Phase>();
            var needsRepair = false;
            try
            {
                // Empty/truncated files can be left behind by an interrupted old
                // save. A JSON null is also not a usable collection.
                var entries = JArray.Parse(text);
                foreach (var entry in entries)
                {
                    try
                    {
                        var phase = entry.ToObject<Phase>();
                        if (phase == null || string.IsNullOrWhiteSpace(phase.PhaseSource))
                            throw new JsonSerializationException("Missing phase source.");
                        phase.SetSource();
                        phase.StartArgs ??= new PhaseArgs();
                        phase.EndArgs ??= new PhaseArgs();
                        phases.Add(phase);
                    }
                    catch (JsonException) { needsRepair = true; }
                }
            }
            catch (JsonException) { needsRepair = true; }

            if (needsRepair)
            {
                // Preserve the original before replacing it, including any custom
                // definitions that could not be parsed. Keep valid array entries.
                var backup = path + ".invalid-" + Guid.NewGuid().ToString("N") + ".bak";
                File.Copy(path, backup);
                WritePhases(path, phases);
                Console.Error.WriteLine($"Recovered phase settings. Original saved to: {backup}");
            }
            return phases;
        }

        internal static void WritePhases(string path, IEnumerable<Phase> phases)
        {
            var json = JsonConvert.SerializeObject(phases.ToArray());
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
                        writer.Write(json);
                    stream.Flush(flushToDisk: true);
                }
                // Same-directory rename replaces the complete document atomically.
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
