using System.Linq;
using Pets.Data;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>
    /// Pure shop-transaction logic — no MonoBehaviour/scene dependency, so it gets fast EditMode
    /// tests instead of PlayMode tests, per CLAUDE.md's "plain C# where scene attachment isn't
    /// needed" convention.
    /// </summary>
    public static class ShopEconomy
    {
        public static void StartRun(RunState state, ShopConfig config, CreatureLibrary library)
        {
            state.Gold = config.StartingGold;
            state.Lives = config.StartingLives;
            state.Round = 1;
            state.Phase = GamePhase.Shop;
            state.Victory = false;
            state.Board.Clear();
            state.ShopSlots.Clear();
            for (int i = 0; i < config.ShopSize; i++)
            {
                state.ShopSlots.Add(new ShopSlot());
            }
            RefreshShop(state, config, library, includeFrozen: true);
        }

        public static void StartShopPhase(RunState state, ShopConfig config, CreatureLibrary library)
        {
            state.Gold = config.GoldPerRound;
            state.Phase = GamePhase.Shop;
            FireOnTurnStart(state);
            RefreshShop(state, config, library, includeFrozen: false);
        }

        /// <summary>Fires each board creature's OnTurnStart abilities, front-to-back.</summary>
        public static void FireOnTurnStart(RunState state)
        {
            foreach (var creature in state.Board)
            {
                FireShopTrigger(state, TriggerType.OnTurnStart, creature);
            }
        }

        public static void RefreshShop(RunState state, ShopConfig config, CreatureLibrary library, bool includeFrozen)
        {
            var pool = library.GetByMaxTier(config.TierForRound(state.Round));
            if (pool.Count == 0)
            {
                return;
            }
            foreach (var slot in state.ShopSlots)
            {
                if (slot.Frozen && !includeFrozen)
                {
                    continue;
                }
                slot.Offer = pool[Random.Range(0, pool.Count)];
                slot.Frozen = false;
            }
        }

        public static bool Buy(RunState state, ShopConfig config, int shopIndex)
        {
            if (shopIndex < 0 || shopIndex >= state.ShopSlots.Count)
            {
                return false;
            }
            var slot = state.ShopSlots[shopIndex];
            if (slot.Offer == null || state.Board.Count >= config.BoardMaxSize)
            {
                return false;
            }
            int cost = config.BuyCost(slot.Offer.Tier);
            if (state.Gold < cost)
            {
                return false;
            }

            state.Gold -= cost;
            var bought = new BoardCreature { Definition = slot.Offer, Level = 1 };
            state.Board.Add(bought);
            slot.Offer = null;
            slot.Frozen = false;

            FireShopTrigger(state, TriggerType.OnBuy, bought);
            TryCombineAll(state, config);
            return true;
        }

        public static bool Sell(RunState state, ShopConfig config, int boardIndex)
        {
            if (boardIndex < 0 || boardIndex >= state.Board.Count)
            {
                return false;
            }
            var creature = state.Board[boardIndex];
            // Fired before removal so Self/RandomAlly resolve against the still-intact board.
            FireShopTrigger(state, TriggerType.OnSell, creature);
            state.Gold += config.SellRefund(creature.Definition.Tier) * creature.Level;
            state.Board.RemoveAt(boardIndex);
            return true;
        }

        public static bool Reroll(RunState state, ShopConfig config, CreatureLibrary library)
        {
            if (state.Gold < config.RerollCost)
            {
                return false;
            }
            state.Gold -= config.RerollCost;
            RefreshShop(state, config, library, includeFrozen: false);
            return true;
        }

        public static bool ToggleFreeze(RunState state, int shopIndex)
        {
            if (shopIndex < 0 || shopIndex >= state.ShopSlots.Count)
            {
                return false;
            }
            var slot = state.ShopSlots[shopIndex];
            if (slot.Offer == null)
            {
                return false;
            }
            slot.Frozen = !slot.Frozen;
            return true;
        }

        public static bool MoveBoardCreature(RunState state, int index, int direction)
        {
            int target = index + direction;
            if (index < 0 || index >= state.Board.Count || target < 0 || target >= state.Board.Count)
            {
                return false;
            }
            (state.Board[index], state.Board[target]) = (state.Board[target], state.Board[index]);
            return true;
        }

        /// <summary>Scans for 3-of-a-kind at the same level and merges to level+1, capped at MaxLevel, repeating until stable.</summary>
        public static void TryCombineAll(RunState state, ShopConfig config)
        {
            bool merged;
            do
            {
                merged = false;
                for (int level = 1; level < config.MaxLevel; level++)
                {
                    var indexed = state.Board.Select((creature, index) => (creature, index));
                    var group = indexed
                        .Where(x => x.creature.Level == level)
                        .GroupBy(x => x.creature.Definition.Id)
                        .FirstOrDefault(g => g.Count() >= 3);

                    if (group == null)
                    {
                        continue;
                    }

                    var triple = group.Take(3).ToList();
                    int insertIndex = triple[0].index;
                    var definition = triple[0].creature.Definition;

                    foreach (var item in triple.OrderByDescending(x => x.index))
                    {
                        state.Board.RemoveAt(item.index);
                    }

                    var leveledUp = new BoardCreature { Definition = definition, Level = level + 1 };
                    state.Board.Insert(insertIndex, leveledUp);
                    FireShopTrigger(state, TriggerType.OnLevelUp, leveledUp);
                    merged = true;
                    break;
                }
            } while (merged);
        }

        /// <summary>
        /// Applies every effect of source's abilities matching trigger. Shop-context vocabulary:
        /// GainGold adds to RunState.Gold; BuffAttack/BuffHealth add a permanent bonus to the
        /// resolved target's BoardCreature (Self or RandomAlly = a random other board creature —
        /// RandomEnemy/FrontEnemy have no meaning in the shop and resolve to no target).
        /// DealDamage/Heal/Summon are battle-only and are no-ops here. See content-schema.md's
        /// shop-phase trigger section.
        /// </summary>
        private static void FireShopTrigger(RunState state, TriggerType trigger, BoardCreature source)
        {
            foreach (var ability in source.Definition.Abilities)
            {
                if (ability == null || ability.Trigger != trigger)
                {
                    continue;
                }
                foreach (var effect in ability.Effects)
                {
                    ApplyShopEffect(state, effect, source);
                }
            }
        }

        private static void ApplyShopEffect(RunState state, EffectDefinition effect, BoardCreature source)
        {
            switch (effect.Type)
            {
                case EffectType.GainGold:
                    state.Gold += effect.Amount;
                    break;
                case EffectType.BuffAttack:
                case EffectType.BuffHealth:
                    var target = ResolveShopTarget(state, effect.Target, source);
                    if (target == null)
                    {
                        break;
                    }
                    if (effect.Type == EffectType.BuffAttack)
                    {
                        target.BonusAttack += effect.Amount;
                    }
                    else
                    {
                        target.BonusHealth += effect.Amount;
                    }
                    break;
                default:
                    break;
            }
        }

        private static BoardCreature ResolveShopTarget(RunState state, TargetSelector selector, BoardCreature source)
        {
            switch (selector)
            {
                case TargetSelector.Self:
                    return source;
                case TargetSelector.RandomAlly:
                    var others = state.Board.Where(c => c != source).ToList();
                    return others.Count == 0 ? null : others[Random.Range(0, others.Count)];
                default:
                    return null;
            }
        }
    }
}
