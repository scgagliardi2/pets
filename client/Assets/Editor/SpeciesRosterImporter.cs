using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using Pets.Data;
using Pets.Simulation;

namespace Pets.EditorTools
{
    /// <summary>The xlsx → ScriptableObject content-import pipeline PLAN.md §8 describes: turns
    /// every row of <c>docs/pokemon_stats_unique.xlsx</c> into a
    /// <see cref="PokemonSpeciesDefinitionAsset"/> under Assets/Content/Species and registers the
    /// lot in <see cref="PokemonSpeciesLibrary"/>.
    ///
    /// It exists because the first 28 species were hand-authored asset by asset, which was fine at
    /// 28 and is not fine at 183 — the sheet is the stated source of truth for names/types/stats,
    /// and hand-typing it is exactly how an asset silently diverges from it.
    ///
    /// **Idempotent, and deliberately non-destructive.** Re-running it over a clean tree changes
    /// nothing. It writes only the fields the sheet actually owns (DisplayName, typing, the three
    /// base stats, the Id-derived sprite reference, and the Legendary flag); anything authored by
    /// hand — an existing Passive assignment, an evolution link — is left exactly as it is, so the
    /// hand-authored passives on the original curated species survive a re-import. A species with
    /// no passive yet gets its Type1's default from <see cref="DefaultPassiveIdByType"/>, since
    /// ContentIntegrityTests (rightly) refuses a species the battle sim can't resolve a passive
    /// for. It never deletes a species asset: a row disappearing from the sheet is reported, not
    /// acted on.
    ///
    /// Run it from <c>Pets &gt; Content &gt; Import Species From Roster Sheet</c>, or headlessly
    /// via <c>-executeMethod Pets.EditorTools.SpeciesRosterImporter.Import</c>.</summary>
    public static class SpeciesRosterImporter
    {
        private const string SpeciesFolder = "Assets/Content/Species";
        private const string SpeciesLibraryPath = "Assets/Content/PokemonSpeciesLibrary.asset";
        private const string PassiveLibraryPath = "Assets/Content/PassiveLibrary.asset";
        private const string ArtFolder = "Assets/Art/Pokemon";

        /// <summary>Sheet columns, by their spreadsheet letter. Read by letter rather than by
        /// position in the row: a row's cells are only present when they hold a value, so an empty
        /// "Type 2" simply isn't there and counting cells would shift every later column.</summary>
        private const string NameColumn = "A";
        private const string Type1Column = "B";
        private const string Type2Column = "C";
        private const string AttackColumn = "E";
        private const string HealthColumn = "F";
        private const string SpeedColumn = "G";

        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        /// <summary>The roster sheet has no rarity column. PLAN.md §8 records the carried-forward
        /// assumption that these seven are Legendary-tier, so that lives here rather than being
        /// re-decided per import.</summary>
        private static readonly HashSet<string> LegendaryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mew", "Mewtwo", "Rayquaza", "Ho-Oh", "Lugia", "Kyogre", "Groudon",
        };

        /// <summary>The passive a newly-imported species gets, chosen by its primary type.
        ///
        /// Passives are hand-authored content (design doc §11's Type-flavor seeds) and the sheet's
        /// Ability column is empty, so an import has to pick *something* — one shared passive per
        /// type is the honest placeholder, and it's what the curated 28 already did informally
        /// (geodude-stone-guard is on Geodude, Diglett, Onix and Snorlax). Giving 155 species a
        /// bespoke passive each is the content work Phase 2 calls for; this table is what keeps
        /// them playable until then. Every entry must resolve through PassiveLibrary — there's a
        /// check below, and ContentIntegrityTests guards it afterwards.</summary>
        private static readonly Dictionary<PokemonType, string> DefaultPassiveIdByType = new Dictionary<PokemonType, string>
        {
            { PokemonType.Normal, "eevee-adaptability" },
            { PokemonType.Fire, "charmander-ember-burst" },
            { PokemonType.Water, "squirtle-shell-guard" },
            { PokemonType.Electric, "pikachu-static-shock" },
            { PokemonType.Grass, "bulbasaur-vine-drain" },
            { PokemonType.Ice, "swinub-permafrost" },
            { PokemonType.Fighting, "machop-power-surge" },
            { PokemonType.Poison, "zubat-bite" },
            { PokemonType.Ground, "trapinch-sand-tomb" },
            { PokemonType.Flying, "pidgey-tailwind" },
            { PokemonType.Psychic, "abra-quick-focus" },
            { PokemonType.Bug, "caterpie-swarm-scurry" },
            { PokemonType.Rock, "geodude-stone-guard" },
            { PokemonType.Ghost, "gastly-cursed-touch" },
            { PokemonType.Dragon, "dratini-coil-up" },
            { PokemonType.Dark, "houndour-night-ambush" },
            { PokemonType.Steel, "aron-iron-hide" },
            { PokemonType.Fairy, "clefairy-moonlight-glow" },
        };

        /// <summary>One row of the sheet, already joined to its National Dex id. Public, along
        /// with <see cref="ReadRoster"/>, so RosterImportTests can compare the sheet the importer
        /// reads against the assets it produced — the check that catches an edited sheet nobody
        /// re-imported, which is otherwise silent.</summary>
        public struct RosterRow
        {
            public string SheetName;
            public string DisplayName;
            public int Id;
            public PokemonType Type1;
            public bool HasSecondType;
            public PokemonType Type2;
            public int Attack;
            public int Health;
            public int Speed;
        }

        [MenuItem("Pets/Content/Import Species From Roster Sheet")]
        public static void Import()
        {
            List<RosterRow> roster;
            try
            {
                roster = ReadRoster();
            }
            catch (Exception error)
            {
                Debug.LogError($"Roster import failed before touching any asset: {error.Message}");
                return;
            }

            var passiveLibrary = AssetDatabase.LoadAssetAtPath<PassiveLibrary>(PassiveLibraryPath);
            if (passiveLibrary == null)
            {
                Debug.LogError($"Roster import failed: no PassiveLibrary at {PassiveLibraryPath}.");
                return;
            }

            var missingPassives = DefaultPassiveIdByType
                .Where(pair => passiveLibrary.GetById(pair.Value) == null)
                .Select(pair => $"{pair.Key} -> '{pair.Value}'")
                .ToArray();
            if (missingPassives.Length > 0)
            {
                Debug.LogError("Roster import failed: DefaultPassiveIdByType names passives that aren't in " +
                               $"PassiveLibrary — {string.Join(", ", missingPassives)}. Author them under " +
                               "Assets/Content/Passives and register them first.");
                return;
            }

            Directory.CreateDirectory(SpeciesFolder);

            var created = new List<string>();
            var updated = new List<string>();
            int unchanged = 0;
            var missingArt = new List<string>();

            // Batched: without this, each CreateAsset kicks off its own import, which at 183
            // species turns a two-second run into a minute of churn.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in roster)
                {
                    string assetPath = $"{SpeciesFolder}/{AssetFileName(entry.DisplayName)}.asset";
                    var species = AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>(assetPath);
                    bool isNew = species == null;
                    if (isNew)
                    {
                        species = ScriptableObject.CreateInstance<PokemonSpeciesDefinitionAsset>();
                    }

                    bool changed = ApplySheetFields(species, entry);
                    changed |= AssignSpriteIfMissing(species, entry, missingArt);
                    changed |= AssignDefaultPassiveIfMissing(species, passiveLibrary);

                    if (isNew)
                    {
                        AssetDatabase.CreateAsset(species, assetPath);
                        created.Add(entry.DisplayName);
                    }
                    else if (changed)
                    {
                        EditorUtility.SetDirty(species);
                        updated.Add(entry.DisplayName);
                    }
                    else
                    {
                        unchanged++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int registered = RebuildLibrary(roster);

            var summary = new StringBuilder();
            summary.AppendLine($"Roster import: {roster.Count} sheet rows -> {created.Count} created, " +
                               $"{updated.Count} updated, {unchanged} unchanged; {registered} registered in " +
                               "PokemonSpeciesLibrary.");
            if (created.Count > 0)
            {
                summary.AppendLine("Created: " + string.Join(", ", created));
            }
            if (updated.Count > 0)
            {
                summary.AppendLine("Updated: " + string.Join(", ", updated));
            }
            Debug.Log(summary.ToString().TrimEnd());

            if (missingArt.Count > 0)
            {
                Debug.LogError($"{missingArt.Count} species have no cached artwork and will render as empty " +
                               $"cards — expected {ArtFolder}/{{id}}.png for: {string.Join(", ", missingArt)}");
            }
        }

        /// <summary>Writes the fields the sheet owns, and reports whether any of them actually
        /// changed — so a re-run over an up-to-date tree dirties nothing and the log says "183
        /// unchanged" rather than rewriting every asset (and its timestamp) for no reason.</summary>
        private static bool ApplySheetFields(PokemonSpeciesDefinitionAsset species, RosterRow entry)
        {
            bool changed =
                species.Id != entry.Id ||
                species.DisplayName != entry.DisplayName ||
                species.Type1 != entry.Type1 ||
                species.HasSecondType != entry.HasSecondType ||
                (entry.HasSecondType && species.Type2 != entry.Type2) ||
                species.BaseAttack != entry.Attack ||
                species.BaseHealth != entry.Health ||
                species.BaseSpeed != entry.Speed ||
                species.IsLegendary != LegendaryNames.Contains(entry.SheetName);

            species.Id = entry.Id;
            species.DisplayName = entry.DisplayName;
            species.Type1 = entry.Type1;
            species.HasSecondType = entry.HasSecondType;
            // Left at whatever it was when there's no second type, rather than forced to Normal:
            // HasSecondType is what every reader gates on, and rewriting the dead field would dirty
            // assets that are otherwise identical.
            if (entry.HasSecondType)
            {
                species.Type2 = entry.Type2;
            }
            species.BaseAttack = entry.Attack;
            species.BaseHealth = entry.Health;
            species.BaseSpeed = entry.Speed;
            species.IsLegendary = LegendaryNames.Contains(entry.SheetName);
            return changed;
        }

        /// <summary>All 183 roster sprites are already cached under Assets/Art/Pokemon by id (see
        /// Assets/Art/README.md), so this is a lookup, not a fetch. Only fills an empty reference,
        /// leaving room for a hand-picked alternate sprite to stick.</summary>
        private static bool AssignSpriteIfMissing(PokemonSpeciesDefinitionAsset species, RosterRow entry, List<string> missingArt)
        {
            if (species.Sprite != null)
            {
                return false;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{entry.Id}.png");
            if (sprite == null)
            {
                missingArt.Add($"{entry.DisplayName} (id {entry.Id})");
                return false;
            }

            species.Sprite = sprite;
            return true;
        }

        private static bool AssignDefaultPassiveIfMissing(PokemonSpeciesDefinitionAsset species, PassiveLibrary passiveLibrary)
        {
            if (species.Passive != null)
            {
                return false;
            }

            species.Passive = passiveLibrary.GetById(DefaultPassiveIdByType[species.Type1]);
            return true;
        }

        /// <summary>Repoints PokemonSpeciesLibrary at every species asset on disk, ordered by Id.
        ///
        /// Rebuilt from the folder rather than appended to: an asset that exists but isn't in the
        /// library is invisible at runtime (the exact failure ContentIntegrityTests guards), and
        /// rebuilding is the only version of this that can't drift. Dex order rather than sheet
        /// order because that's the order the Pokédex screen and Character Select present, and
        /// RunBootstrapper's fallback starters are AllSpecies[0] and [1].</summary>
        private static int RebuildLibrary(List<RosterRow> roster)
        {
            var library = AssetDatabase.LoadAssetAtPath<PokemonSpeciesLibrary>(SpeciesLibraryPath);
            if (library == null)
            {
                Debug.LogError($"No PokemonSpeciesLibrary at {SpeciesLibraryPath} — species were written but " +
                               "nothing registered them, so they're invisible at runtime.");
                return 0;
            }

            var onDisk = AssetDatabase.FindAssets($"t:{nameof(PokemonSpeciesDefinitionAsset)}", new[] { SpeciesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PokemonSpeciesDefinitionAsset>)
                .Where(s => s != null)
                .OrderBy(s => s.Id)
                .ToList();

            var rosterIds = new HashSet<int>(roster.Select(r => r.Id));
            var orphans = onDisk.Where(s => !rosterIds.Contains(s.Id)).Select(s => s.DisplayName).ToArray();
            if (orphans.Length > 0)
            {
                Debug.LogWarning($"{orphans.Length} species assets have no row in the roster sheet and were left " +
                                 $"in place: {string.Join(", ", orphans)}. Delete them by hand if the sheet is right.");
            }

            library.AllSpecies = onDisk;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return onDisk.Count;
        }

        /// <summary>Asset file name for a species: lowercased, with everything that isn't a letter
        /// or digit dropped. Matches how the hand-authored assets were named (Nidoran-F lives at
        /// nidoranf.asset), so importing over the curated 28 updates them in place instead of
        /// creating a second copy under a different name.</summary>
        private static string AssetFileName(string displayName) =>
            new string(displayName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        /// <summary>Sheet spelling → the name shown in-game. Only the gendered Nidoran pair needs
        /// it: the sheet writes "Nidoran (M)" / "Nidoran (F)", and the curated asset that already
        /// exists for the female is "Nidoran-F".</summary>
        private static string ToDisplayName(string sheetName)
        {
            if (sheetName.EndsWith("(M)", StringComparison.Ordinal) || sheetName.EndsWith("(F)", StringComparison.Ordinal))
            {
                return sheetName.Substring(0, sheetName.Length - 3).TrimEnd() + "-" + sheetName[sheetName.Length - 2];
            }
            return sheetName;
        }

        // ---------------------------------------------------------------------------------------
        // Reading the sheet
        // ---------------------------------------------------------------------------------------

        private static string RepoPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../..", relative));

        /// <summary>Reads the sheet and joins it to the cached National Dex ids.
        ///
        /// The sheet carries no id column, and the id is what everything downstream is keyed by
        /// (the artwork file name, PokemonSpeciesLibrary.GetById). tools/fetch_roster_ids.py
        /// resolves name → id against PokeAPI once and caches the answer, so an import never needs
        /// the network — see PLAN.md §8/§9 on caching rather than hitting PokeAPI.</summary>
        public static List<RosterRow> ReadRoster()
        {
            string sheetPath = RepoPath("docs/pokemon_stats_unique.xlsx");
            string idsPath = RepoPath("docs/roster_pokeapi_ids.json");

            if (!File.Exists(sheetPath))
            {
                throw new FileNotFoundException($"Roster sheet not found at {sheetPath}.");
            }
            if (!File.Exists(idsPath))
            {
                throw new FileNotFoundException(
                    $"National Dex id cache not found at {idsPath}. Run tools/fetch_roster_ids.py.");
            }
            if (File.GetLastWriteTimeUtc(idsPath) < File.GetLastWriteTimeUtc(sheetPath))
            {
                Debug.LogWarning("docs/roster_pokeapi_ids.json is older than the roster sheet — if the sheet " +
                                 "gained or renamed a species, re-run tools/fetch_roster_ids.py first.");
            }

            var idsByName = ReadIdCache(idsPath);
            var rows = ReadSheetRows(sheetPath);

            var roster = new List<RosterRow>();
            var seenIds = new HashSet<int>();
            foreach (var row in rows)
            {
                string sheetName = Value(row, NameColumn);
                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    continue;
                }
                if (!idsByName.TryGetValue(sheetName, out int id))
                {
                    throw new InvalidDataException(
                        $"'{sheetName}' has no id in docs/roster_pokeapi_ids.json — re-run tools/fetch_roster_ids.py.");
                }
                if (!seenIds.Add(id))
                {
                    throw new InvalidDataException($"Two sheet rows resolve to id {id} ('{sheetName}' is the second).");
                }

                string type2 = Value(row, Type2Column);
                roster.Add(new RosterRow
                {
                    SheetName = sheetName,
                    DisplayName = ToDisplayName(sheetName),
                    Id = id,
                    Type1 = ParseType(Value(row, Type1Column), sheetName),
                    HasSecondType = !string.IsNullOrWhiteSpace(type2),
                    Type2 = string.IsNullOrWhiteSpace(type2) ? default : ParseType(type2, sheetName),
                    Attack = ParseStat(Value(row, AttackColumn), sheetName, "Base Attack"),
                    Health = ParseStat(Value(row, HealthColumn), sheetName, "Base HP"),
                    Speed = ParseStat(Value(row, SpeedColumn), sheetName, "Base Speed"),
                });
            }

            if (roster.Count == 0)
            {
                throw new InvalidDataException("The roster sheet parsed to zero species rows.");
            }
            return roster;
        }

        [Serializable]
        private struct IdCacheEntry
        {
            public string name;
            public int id;
        }

        [Serializable]
        private struct IdCache
        {
            public IdCacheEntry[] species;
        }

        private static Dictionary<string, int> ReadIdCache(string path)
        {
            var cache = JsonUtility.FromJson<IdCache>(File.ReadAllText(path));
            if (cache.species == null || cache.species.Length == 0)
            {
                throw new InvalidDataException($"{path} holds no species entries.");
            }
            return cache.species.ToDictionary(e => e.name, e => e.id);
        }

        /// <summary>Reads the first worksheet as a list of (column letter → cell text) rows, header
        /// row dropped.
        ///
        /// A deliberately minimal xlsx reader rather than a spreadsheet library: an .xlsx is a zip
        /// of XML, this needs seven columns of text out of one sheet, and a dependency (plus its
        /// meta files and licence) for that would be the larger cost. It handles both string
        /// encodings the format allows — inline strings, which is what the current sheet uses, and
        /// the shared-string table Excel writes on a re-save — because otherwise opening and saving
        /// the sheet in Excel would silently turn every name into a number.</summary>
        private static List<Dictionary<string, string>> ReadSheetRows(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var sharedStrings = ReadSharedStrings(archive);
                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
                    ?? throw new InvalidDataException($"{path} has no xl/worksheets/sheet1.xml.");

                XDocument document;
                using (var sheetStream = sheetEntry.Open())
                {
                    document = XDocument.Load(sheetStream);
                }

                var rows = new List<Dictionary<string, string>>();
                var sheetData = document.Root?.Element(Ns + "sheetData");
                if (sheetData == null)
                {
                    throw new InvalidDataException($"{path}'s worksheet has no sheetData.");
                }

                bool isHeader = true;
                foreach (var row in sheetData.Elements(Ns + "row"))
                {
                    if (isHeader)
                    {
                        isHeader = false;
                        continue;
                    }

                    var cells = new Dictionary<string, string>();
                    foreach (var cell in row.Elements(Ns + "c"))
                    {
                        string reference = (string)cell.Attribute("r") ?? string.Empty;
                        string column = new string(reference.TakeWhile(char.IsLetter).ToArray());
                        if (column.Length == 0)
                        {
                            continue;
                        }
                        cells[column] = CellText(cell, sharedStrings);
                    }
                    rows.Add(cells);
                }
                return rows;
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            using (var stream = entry.Open())
            {
                var document = XDocument.Load(stream);
                return document.Root?
                    .Elements(Ns + "si")
                    .Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)))
                    .ToList() ?? new List<string>();
            }
        }

        private static string CellText(XElement cell, List<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");
            switch (type)
            {
                case "inlineStr":
                    return string.Concat(cell.Descendants(Ns + "t").Select(t => t.Value)).Trim();
                case "s":
                {
                    string raw = cell.Element(Ns + "v")?.Value;
                    return int.TryParse(raw, out int index) && index >= 0 && index < sharedStrings.Count
                        ? sharedStrings[index].Trim()
                        : string.Empty;
                }
                case "str":
                    return (cell.Element(Ns + "v")?.Value ?? string.Empty).Trim();
                default:
                    return (cell.Element(Ns + "v")?.Value ?? string.Empty).Trim();
            }
        }

        private static string Value(Dictionary<string, string> row, string column) =>
            row.TryGetValue(column, out string value) ? value : string.Empty;

        private static PokemonType ParseType(string raw, string speciesName)
        {
            if (!Enum.TryParse(raw, ignoreCase: true, out PokemonType type))
            {
                throw new InvalidDataException($"{speciesName} has an unrecognized type '{raw}'.");
            }
            return type;
        }

        private static int ParseStat(string raw, string speciesName, string column)
        {
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0)
            {
                throw new InvalidDataException($"{speciesName} has a non-positive or unparseable {column}: '{raw}'.");
            }
            return value;
        }
    }
}
