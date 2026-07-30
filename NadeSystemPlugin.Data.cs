using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using RayTraceAPI;

namespace NadeSystem;

public partial class NadeSystemPlugin : BasePlugin
{
    // ═══════════════════════════════════════════════════════════
    //  DB I/O
    //  Reads every *.json in the grenades/ folder.
    //  Each file is a JSON array produced by convert_lineups.py.
    //  Expected filename convention: <mapname>_<grenadeType>.json
    //  but the mapName field inside each entry is authoritative.
    // ═══════════════════════════════════════════════════════════

    // * Loads and normalizes grenade records for the active map
    private void LoadDb()
    {
        int loaded = 0;
        foreach (var file in Directory.GetFiles(DataDir, "*.json"))
        {
            try
            {
                var text = File.ReadAllText(file);
                var list = JsonSerializer.Deserialize<List<GrenadeData>>(text);
                if (list == null) continue;
                foreach (var entry in list)
                {
                    entry.Description ??= "";
                    // Normalize once at load so hot paths can compare without ToLower()
                    entry.GrenadeType = (entry.GrenadeType ?? "").ToLowerInvariant();
                    // Rewrite grenadeType to "decoy" if description contains "decoy"
                    if (entry.Description.Contains("decoy", StringComparison.OrdinalIgnoreCase))
                        entry.GrenadeType = "decoy";
                    // Tags for nades that only trigger at round start
                    if (entry.Description.StartsWith("CT", StringComparison.OrdinalIgnoreCase))
                        entry.TeamTag = "CT";
                    else if (entry.Description.StartsWith("T", StringComparison.OrdinalIgnoreCase))
                        entry.TeamTag = "T";
                    else
                        entry.TeamTag = "";
                }
                _db.AddRange(list);
                loaded += list.Count;
            }
            catch (Exception ex)
            {
                Server.PrintToConsole(
                    $"[NadeSystem] Failed to load {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        // Pre-filter to current map
        _mapNades = _db
            .Where(g => string.Equals(g.MapName, Server.MapName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Server.PrintToConsole($"[NadeSystem] Loaded {loaded} grenades from {DataDir}");
    }

}
