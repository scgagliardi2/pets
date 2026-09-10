using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Pets.Data;
using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>
    /// JsonUtility-based local save — Application.persistentDataPath/save.json. Stores creature
    /// ids (resolved back through CreatureLibrary on load), same pattern as ContentJsonExporter.
    /// Also owns a separate cross-run history log (run-history.json), unrelated to the live-run
    /// save above — see AppendHistory/LoadHistory.
    /// </summary>
    public static class SaveSystem
    {
        private const int MaxHistoryEntries = 100;

        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
        private static string HistoryPath => Path.Combine(Application.persistentDataPath, "run-history.json");

        public static void Save(RunState state)
        {
            var dto = new SaveDto
            {
                gold = state.Gold,
                lives = state.Lives,
                round = state.Round,
                phase = state.Phase.ToString(),
                victory = state.Victory,
                board = state.Board.Select(c => new SaveCreature { creatureId = c.Definition.Id, level = c.Level, bonusAttack = c.BonusAttack, bonusHealth = c.BonusHealth }).ToArray(),
                shopSlots = state.ShopSlots.Select(s => new SaveShopSlot { creatureId = s.Offer != null ? s.Offer.Id : "", frozen = s.Frozen }).ToArray(),
            };
            File.WriteAllText(SavePath, JsonUtility.ToJson(dto));
        }

        public static bool TryLoad(CreatureLibrary library, out RunState state)
        {
            state = null;
            if (!File.Exists(SavePath))
            {
                return false;
            }

            var dto = JsonUtility.FromJson<SaveDto>(File.ReadAllText(SavePath));
            if (dto == null)
            {
                return false;
            }

            state = new RunState
            {
                Gold = dto.gold,
                Lives = dto.lives,
                Round = dto.round,
                Phase = Enum.TryParse(dto.phase, out GamePhase phase) ? phase : GamePhase.Shop,
                Victory = dto.victory,
            };

            foreach (var c in dto.board ?? Array.Empty<SaveCreature>())
            {
                var definition = library.GetById(c.creatureId);
                if (definition != null)
                {
                    state.Board.Add(new BoardCreature { Definition = definition, Level = c.level, BonusAttack = c.bonusAttack, BonusHealth = c.bonusHealth });
                }
            }

            foreach (var s in dto.shopSlots ?? Array.Empty<SaveShopSlot>())
            {
                state.ShopSlots.Add(new ShopSlot
                {
                    Offer = string.IsNullOrEmpty(s.creatureId) ? null : library.GetById(s.creatureId),
                    Frozen = s.frozen,
                });
            }

            return true;
        }

        public static void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }

        public static void AppendHistory(RunHistoryEntry entry)
        {
            var entries = LoadHistory();
            entries.Add(entry);
            if (entries.Count > MaxHistoryEntries)
            {
                entries.RemoveRange(0, entries.Count - MaxHistoryEntries);
            }

            var dto = new HistoryDto
            {
                entries = entries.Select(e => new HistoryEntryDto { completedAtUtc = e.CompletedAtUtc, roundReached = e.RoundReached, victory = e.Victory }).ToArray(),
            };
            File.WriteAllText(HistoryPath, JsonUtility.ToJson(dto));
        }

        public static List<RunHistoryEntry> LoadHistory()
        {
            if (!File.Exists(HistoryPath))
            {
                return new List<RunHistoryEntry>();
            }

            var dto = JsonUtility.FromJson<HistoryDto>(File.ReadAllText(HistoryPath));
            if (dto?.entries == null)
            {
                return new List<RunHistoryEntry>();
            }

            return dto.entries.Select(e => new RunHistoryEntry { CompletedAtUtc = e.completedAtUtc, RoundReached = e.roundReached, Victory = e.victory }).ToList();
        }

        public static void DeleteHistory()
        {
            if (File.Exists(HistoryPath))
            {
                File.Delete(HistoryPath);
            }
        }

        [Serializable]
        private sealed class SaveDto
        {
            public int gold;
            public int lives;
            public int round;
            public string phase;
            public bool victory;
            public SaveCreature[] board;
            public SaveShopSlot[] shopSlots;
        }

        [Serializable]
        private sealed class SaveCreature
        {
            public string creatureId;
            public int level;
            public int bonusAttack;
            public int bonusHealth;
        }

        [Serializable]
        private sealed class SaveShopSlot
        {
            public string creatureId;
            public bool frozen;
        }

        [Serializable]
        private sealed class HistoryDto
        {
            public HistoryEntryDto[] entries;
        }

        [Serializable]
        private sealed class HistoryEntryDto
        {
            public string completedAtUtc;
            public int roundReached;
            public bool victory;
        }
    }
}
