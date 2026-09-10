using System.Collections.Generic;
using System.IO;
using Pets.Data;
using Pets.Simulation;
using UnityEditor;
using UnityEngine;

namespace Pets.Editor
{
    /// <summary>
    /// One-off generator for the Phase 1 starter roster (docs/content-schema.md,
    /// docs/battle-sim-spec.md's worked examples). Run via the menu item below, or in batch mode
    /// with `-executeMethod Pets.Editor.ContentSeeder.GenerateStarterContent`. Idempotent —
    /// re-running updates existing assets in place by id/path rather than duplicating them.
    /// </summary>
    public static class ContentSeeder
    {
        private const string CreaturesPath = "Assets/Content/Creatures";
        private const string AbilitiesPath = "Assets/Content/Abilities";
        private const string BotRosterPath = "Assets/Content/BotRoster";

        [MenuItem("Pets/Generate Starter Content")]
        public static void GenerateStarterContent()
        {
            EnsureFolder("Assets/Content");
            EnsureFolder(CreaturesPath);
            EnsureFolder(AbilitiesPath);
            EnsureFolder(BotRosterPath);

            var burr = CreateCreature("burr-token", "Burr", tier: 0, atk: 1, hp: 1, lvlAtk: 0, lvlHp: 0,
                color: new Color(0.55f, 0.4f, 0.2f));

            var pebblehideFaint = CreateAbility("pebblehide-onfaint", TriggerType.OnFaint,
                Effect(EffectType.DealDamage, TargetSelector.RandomEnemy, 1));
            var sparkletBattleStart = CreateAbility("sparklet-onbattlestart", TriggerType.OnBattleStart,
                Effect(EffectType.BuffAttack, TargetSelector.RandomAlly, 1));
            var mossbackHurt = CreateAbility("mossback-onhurt", TriggerType.OnHurt,
                Effect(EffectType.Heal, TargetSelector.Self, 1));
            var thistlepupFaint = CreateAbility("thistlepup-onfaint", TriggerType.OnFaint,
                SummonEffect(burr));
            var glimmothBattleStart = CreateAbility("glimmoth-onbattlestart", TriggerType.OnBattleStart,
                Effect(EffectType.DealDamage, TargetSelector.FrontEnemy, 2));
            var cragtoiseHurt = CreateAbility("cragtoise-onhurt", TriggerType.OnHurt,
                Effect(EffectType.DealDamage, TargetSelector.RandomEnemy, 2));

            var pebblehide = CreateCreature("pebblehide", "Pebblehide", 1, 2, 3, 2, 2, new Color(0.62f, 0.5f, 0.38f), pebblehideFaint);
            var sparklet = CreateCreature("sparklet", "Sparklet", 1, 3, 1, 2, 2, new Color(1f, 0.85f, 0.25f), sparkletBattleStart);
            var mossback = CreateCreature("mossback", "Mossback", 1, 1, 5, 2, 2, new Color(0.3f, 0.55f, 0.32f), mossbackHurt);
            var thistlepup = CreateCreature("thistlepup", "Thistlepup", 2, 4, 3, 2, 2, new Color(0.72f, 0.3f, 0.28f), thistlepupFaint);
            var glimmoth = CreateCreature("glimmoth", "Glimmoth", 2, 2, 2, 2, 2, new Color(0.52f, 0.3f, 0.78f), glimmothBattleStart);
            var cragtoise = CreateCreature("cragtoise", "Cragtoise", 3, 3, 8, 2, 2, new Color(0.4f, 0.42f, 0.44f), cragtoiseHurt);

            var botTeams = new List<BotTeamDefinition>
            {
                CreateBotTeam(1, (pebblehide, 1)),
                CreateBotTeam(2, (pebblehide, 1), (sparklet, 1)),
                CreateBotTeam(3, (sparklet, 1), (mossback, 1)),
                CreateBotTeam(4, (pebblehide, 2), (mossback, 1), (sparklet, 1)),
                CreateBotTeam(5, (thistlepup, 1), (pebblehide, 1)),
                CreateBotTeam(6, (glimmoth, 1), (thistlepup, 1), (mossback, 1)),
                CreateBotTeam(7, (cragtoise, 1), (glimmoth, 1), (sparklet, 2)),
                CreateBotTeam(8, (cragtoise, 2), (thistlepup, 2), (pebblehide, 2), (mossback, 1)),
            };

            var allCreatures = new List<CreatureDefinition> { burr, pebblehide, sparklet, mossback, thistlepup, glimmoth, cragtoise };

            var library = CreateOrLoadAsset<CreatureLibrary>("Assets/Content/CreatureLibrary.asset");
            library.AllCreatures = allCreatures;
            EditorUtility.SetDirty(library);

            var roster = CreateOrLoadAsset<BotRosterLibrary>("Assets/Content/BotRosterLibrary.asset");
            roster.Rounds = botTeams;
            EditorUtility.SetDirty(roster);

            CreateOrLoadAsset<ShopConfig>("Assets/Content/ShopConfig.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Pets/Generate Starter Content: done.");
        }

        [MenuItem("Pets/Export Starter Content To JSON")]
        public static void ExportStarterContentToJson()
        {
            const string exportPath = "Assets/Content/Exported";
            EnsureFolder(exportPath);

            var creatureGuids = AssetDatabase.FindAssets("t:CreatureDefinition", new[] { CreaturesPath });
            foreach (var guid in creatureGuids)
            {
                var creature = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                File.WriteAllText($"{exportPath}/{creature.Id}.json", ContentJsonExporter.ToJson(creature));
            }

            var botTeamGuids = AssetDatabase.FindAssets("t:BotTeamDefinition", new[] { BotRosterPath });
            foreach (var guid in botTeamGuids)
            {
                var botTeam = AssetDatabase.LoadAssetAtPath<BotTeamDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                File.WriteAllText($"{exportPath}/round-{botTeam.Round:D2}.json", ContentJsonExporter.ToJson(botTeam));
            }

            AssetDatabase.Refresh();
            Debug.Log($"Pets/Export Starter Content To JSON: exported {creatureGuids.Length} creatures, {botTeamGuids.Length} bot teams.");
        }

        private static EffectDefinition Effect(EffectType type, TargetSelector target, int amount)
        {
            return new EffectDefinition { Type = type, Target = target, Amount = amount };
        }

        private static EffectDefinition SummonEffect(CreatureDefinition template)
        {
            return new EffectDefinition { Type = EffectType.Summon, SummonTemplate = template };
        }

        private static AbilityDefinition CreateAbility(string assetName, TriggerType trigger, params EffectDefinition[] effects)
        {
            var asset = CreateOrLoadAsset<AbilityDefinition>($"{AbilitiesPath}/{assetName}.asset");
            asset.Trigger = trigger;
            asset.Effects = new List<EffectDefinition>(effects);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static CreatureDefinition CreateCreature(string id, string displayName, int tier, int atk, int hp, int lvlAtk, int lvlHp, Color color, params AbilityDefinition[] abilities)
        {
            var asset = CreateOrLoadAsset<CreatureDefinition>($"{CreaturesPath}/{id}.asset");
            asset.Id = id;
            asset.DisplayName = displayName;
            asset.Tier = tier;
            asset.BaseAttack = atk;
            asset.BaseHealth = hp;
            asset.LevelAttackBonus = lvlAtk;
            asset.LevelHealthBonus = lvlHp;
            asset.PlaceholderColor = color;
            asset.Abilities = new List<AbilityDefinition>(abilities);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BotTeamDefinition CreateBotTeam(int round, params (CreatureDefinition creature, int level)[] slots)
        {
            var asset = CreateOrLoadAsset<BotTeamDefinition>($"{BotRosterPath}/round-{round:D2}.asset");
            asset.Round = round;
            var list = new List<BotSlot>();
            foreach (var (creature, level) in slots)
            {
                list.Add(new BotSlot { Creature = creature, Level = level });
            }
            asset.Slots = list;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
