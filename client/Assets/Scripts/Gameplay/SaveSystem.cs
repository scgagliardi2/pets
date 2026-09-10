using System;
using System.IO;
using System.Linq;
using Pets.Data;
using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>
    /// JsonUtility-based local save — Application.persistentDataPath/save.json. Stores creature
    /// ids (resolved back through CreatureLibrary on load), same pattern as ContentJsonExporter.
    /// </summary>
    public static class SaveSystem
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

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
    }
}
