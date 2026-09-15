using System;
using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>The four encounters an Event node ("Encounter" on the map) can roll (ADR 0014).</summary>
    public enum RoadEventKind
    {
        LegendarySighting,
        GameCorner,
        TravelingTrader,
        RocketAmbush,
    }

    public sealed class RoadEventChoice
    {
        public string Label;

        /// <summary>False when the run can't take this choice right now (no money for the slots, an
        /// empty bag to hand over). Shown, but dead.</summary>
        public bool Available = true;
    }

    /// <summary>One encounter as rolled for one node: its text, its choices, and whatever it pre-rolled
    /// so the choice text can name it (the Legendary, the trade).</summary>
    public sealed class RoadEvent
    {
        public RoadEventKind Kind;
        public string Title;
        public string Body;
        public List<RoadEventChoice> Choices = new List<RoadEventChoice>();

        /// <summary>The node's seed — every chance an outcome rolls is drawn from it, so an encounter is
        /// as reproducible as the fight a Battle node leads to.</summary>
        public int Seed;

        /// <summary>LegendarySighting: the mon the Challenge choice fights.</summary>
        public PokemonInstance Legendary;

        /// <summary>TravelingTrader: the mon on offer, and which of the run's mons it would replace.</summary>
        public PokemonInstance TradeOffer;
        public RosterGroup TradeGroup;
        public int TradeIndex = -1;

        /// <summary>Set by the first choice resolved, so a second click can't take two.</summary>
        public bool IsResolved;
    }

    /// <summary>What one choice did: the line to show, and — for the one choice that starts a fight —
    /// the line-up to fight and what beating it pays.</summary>
    public sealed class RoadEventOutcome
    {
        public string Message;
        public List<PokemonInstance> BattleLineUp;
        public LegendaryBounty Bounty;

        public bool StartsBattle => BattleLineUp != null && BattleLineUp.Count > 0;
    }

    /// <summary>What beating a Legendary pays on top of an ordinary win.</summary>
    public sealed class LegendaryBounty
    {
        public int Money;
        public string ItemId;
        public string ItemName;
    }

    /// <summary>The Event node's encounters (design doc §5.1's "narrative screen → choose an outcome";
    /// ADR 0014), after Slay the Spire's `?` rooms — a short scene, two or three choices, each a known
    /// trade with at most one coin flip in it:
    /// - **Legendary Sighting** — fight a lone Legendary for money and a held item, or slip away.
    /// - **Game Corner** — gamble money on the slots, or take a small sure thing.
    /// - **Traveling Trader** — swap the run's least-grown mon for a species a tier up, or decline.
    /// - **Team Rocket Ambush** — lose money, lose an item, or risk Morale for an item.
    ///
    /// Plain C# rather than content assets on purpose: four encounters don't earn an effect vocabulary,
    /// and ADR 0014 says when that changes. Nothing here touches a scene — NodeResolutionController shows
    /// the text and forwards the click, and the Battle screen pays a Legendary's bounty.</summary>
    public static class RoadEvents
    {
        public static readonly RoadEventKind[] AllKinds =
        {
            RoadEventKind.LegendarySighting, RoadEventKind.GameCorner, RoadEventKind.TravelingTrader,
            RoadEventKind.RocketAmbush,
        };

        public const int LegendaryBountyMoney = 15;
        public const int SlotsStake = 5;
        public const int SlotsPrize = 20;
        public const int SlotsWinPercent = 50;
        public const int LooseCoins = 4;
        public const int LootWinPercent = 50;

        public const string LegendaryInstancePrefix = "legendary-";

        /// <summary>Rolls which encounter a node is, then builds it.</summary>
        public static RoadEvent Roll(RunState state, PokemonSpeciesLibrary species, ItemLibrary items, int seed)
        {
            var kind = AllKinds[new DeterministicRandom(seed).NextInt(AllKinds.Length)];
            return Build(kind, state, species, items, seed);
        }

        public static RoadEvent Build(RoadEventKind kind, RunState state, PokemonSpeciesLibrary species, ItemLibrary items,
            int seed)
        {
            switch (kind)
            {
                case RoadEventKind.LegendarySighting:
                    return BuildLegendary(state, species, seed) ?? BuildGameCorner(state, seed);
                case RoadEventKind.TravelingTrader:
                    return BuildTrader(state, species, seed);
                case RoadEventKind.RocketAmbush:
                    return BuildRocket(state, items, seed);
                default:
                    return BuildGameCorner(state, seed);
            }
        }

        /// <summary>Applies choice <paramref name="index"/>. Null, changing nothing, if the event was
        /// already resolved or the choice doesn't exist or isn't available.</summary>
        public static RoadEventOutcome Resolve(RoadEvent ev, int index, RunState state, PokemonSpeciesLibrary species,
            ItemLibrary items)
        {
            if (ev == null || state == null || ev.IsResolved || index < 0 || index >= ev.Choices.Count
                || !ev.Choices[index].Available)
            {
                return null;
            }
            ev.IsResolved = true;

            // Salted, so a coin flip isn't correlated with the roll that picked the encounter.
            var rng = new DeterministicRandom(ev.Seed ^ 0x5bd1e995);
            switch (ev.Kind)
            {
                case RoadEventKind.LegendarySighting:
                    return ResolveLegendary(ev, index, species, items, rng);
                case RoadEventKind.TravelingTrader:
                    return ResolveTrader(ev, index, state, species);
                case RoadEventKind.RocketAmbush:
                    return ResolveRocket(index, state, items, rng);
                default:
                    return ResolveGameCorner(index, state, rng);
            }
        }

        /// <summary>Pays a beaten Legendary's bounty into the run.</summary>
        public static void GrantBounty(RunState state, LegendaryBounty bounty)
        {
            if (state == null || bounty == null)
            {
                return;
            }
            state.Money += bounty.Money;
            if (!string.IsNullOrEmpty(bounty.ItemId))
            {
                state.Items.Add(bounty.ItemId);
            }
        }

        /// <summary>Team Rocket's toll: half the run's money, rounded up.</summary>
        public static int Toll(RunState state) => (Math.Max(0, state.Money) + 1) / 2;

        // ---- Legendary Sighting ---------------------------------------------------------------

        private static RoadEvent BuildLegendary(RunState state, PokemonSpeciesLibrary species, int seed)
        {
            var pool = species?.AllSpecies.Where(s => s != null && s.IsLegendary).ToList();
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            var rng = new DeterministicRandom(seed ^ 0x1b873593);
            var legendary = pool[rng.NextInt(pool.Count)];
            // Pitched at the Location's baseline like any other opponent (RunProgression) — what makes
            // it dangerous is its tier, and that it's the one Legendary a run meets before the last Gym.
            var mon = ExperienceResolver.CreateAtExp(legendary, $"{LegendaryInstancePrefix}{seed}",
                RunProgression.BaselineExp(state.BadgeCount), species);

            return new RoadEvent
            {
                Kind = RoadEventKind.LegendarySighting,
                Title = "Legendary Sighting",
                Body = $"{legendary.DisplayName} stands in the path, watching you. A tier {legendary.Tier} Legendary — " +
                       $"few trainers walk away from this fight.\nWin: ${LegendaryBountyMoney} and a held item. Lose: 1 Morale.",
                Seed = seed,
                Legendary = mon,
                Choices =
                {
                    new RoadEventChoice { Label = $"Challenge {legendary.DisplayName}" },
                    new RoadEventChoice { Label = "Slip away" },
                },
            };
        }

        private static RoadEventOutcome ResolveLegendary(RoadEvent ev, int index, PokemonSpeciesLibrary species,
            ItemLibrary items, DeterministicRandom rng)
        {
            string name = species?.GetById(ev.Legendary.SpeciesId)?.DisplayName ?? "The Legendary";
            if (index != 0)
            {
                return new RoadEventOutcome { Message = $"You hold your breath until {name} loses interest and is gone." };
            }

            var item = RandomItem(items, rng);
            return new RoadEventOutcome
            {
                Message = $"{name} turns to face you.",
                BattleLineUp = new List<PokemonInstance> { ev.Legendary },
                Bounty = new LegendaryBounty { Money = LegendaryBountyMoney, ItemId = item?.Id, ItemName = item?.DisplayName },
            };
        }

        // ---- Game Corner ----------------------------------------------------------------------

        private static RoadEvent BuildGameCorner(RunState state, int seed) => new RoadEvent
        {
            Kind = RoadEventKind.GameCorner,
            Title = "Game Corner",
            Body = "A roadside Game Corner hums with lights and jingles. A slot machine blinks at you, " +
                   "and a few coins glint on the floor under it.",
            Seed = seed,
            Choices =
            {
                new RoadEventChoice
                {
                    Label = $"Play the slots (${SlotsStake}, {SlotsWinPercent}% to win ${SlotsPrize})",
                    Available = state.Money >= SlotsStake,
                },
                new RoadEventChoice { Label = $"Pocket the loose coins (+${LooseCoins})" },
            },
        };

        private static RoadEventOutcome ResolveGameCorner(int index, RunState state, DeterministicRandom rng)
        {
            if (index != 0)
            {
                state.Money += LooseCoins;
                return new RoadEventOutcome { Message = $"Nobody's looking. You pocket ${LooseCoins}." };
            }

            state.Money -= SlotsStake;
            if (rng.NextInt(100) < SlotsWinPercent)
            {
                state.Money += SlotsPrize;
                return new RoadEventOutcome { Message = $"7 - 7 - 7! The machine pours out ${SlotsPrize}." };
            }
            return new RoadEventOutcome { Message = $"Cherry, bar, Voltorb. The machine keeps your ${SlotsStake}." };
        }

        // ---- Traveling Trader -----------------------------------------------------------------

        private static RoadEvent BuildTrader(RunState state, PokemonSpeciesLibrary species, int seed)
        {
            var ev = new RoadEvent
            {
                Kind = RoadEventKind.TravelingTrader,
                Title = "Traveling Trader",
                Seed = seed,
            };

            var (group, index) = TradeCandidate(state);
            var yours = index >= 0 ? state.CollectionFor(group)[index] : null;
            var yourSpecies = yours != null ? species?.GetById(yours.SpeciesId) : null;
            var offered = yourSpecies != null ? OfferFor(yours, species, seed) : null;

            if (offered == null)
            {
                ev.Body = "A trader looks over your team, finds nothing to trade for, and tips their hat.";
                ev.Choices.Add(new RoadEventChoice { Label = "Trade", Available = false });
                ev.Choices.Add(new RoadEventChoice { Label = "Move on" });
                return ev;
            }

            ev.TradeGroup = group;
            ev.TradeIndex = index;
            ev.TradeOffer = ExperienceResolver.CreateAtExp(offered, $"trade-{seed}", yours.Exp, species);
            ev.Body = $"A trader has their eye on your {yourSpecies.DisplayName}, and offers a tier {offered.Tier} " +
                      $"{offered.DisplayName} with the same {yours.Exp} EXP in return. Whatever it's holding stays with you.";
            ev.Choices.Add(new RoadEventChoice { Label = $"Trade {yourSpecies.DisplayName} for {offered.DisplayName}" });
            ev.Choices.Add(new RoadEventChoice { Label = "Decline" });
            return ev;
        }

        private static RoadEventOutcome ResolveTrader(RoadEvent ev, int index, RunState state, PokemonSpeciesLibrary species)
        {
            if (index != 0 || ev.TradeOffer == null)
            {
                return new RoadEventOutcome { Message = "The trader shrugs and goes on their way." };
            }

            var collection = state.CollectionFor(ev.TradeGroup);
            if (ev.TradeIndex < 0 || ev.TradeIndex >= collection.Count)
            {
                return new RoadEventOutcome { Message = "The trader shrugs and goes on their way." };
            }

            var given = collection[ev.TradeIndex];
            HeldItems.ReturnToBag(state, given);
            collection[ev.TradeIndex] = ev.TradeOffer;

            string givenName = species?.GetById(given.SpeciesId)?.DisplayName ?? "your Pokémon";
            string gotName = species?.GetById(ev.TradeOffer.SpeciesId)?.DisplayName ?? "a new Pokémon";
            return new RoadEventOutcome { Message = $"You wave goodbye to {givenName}. {gotName} takes its place on your team." };
        }

        /// <summary>The run's least-grown mon — the Box's first on a tie, since that's the one the player
        /// is least attached to. The replacement takes its exact slot, so trading the only mon in the
        /// line-up still leaves the line-up with one.</summary>
        public static (RosterGroup Group, int Index) TradeCandidate(RunState state)
        {
            var best = (Group: RosterGroup.Box, Index: -1);
            int bestExp = int.MaxValue;
            foreach (var group in new[] { RosterGroup.Box, RosterGroup.Party })
            {
                var collection = state.CollectionFor(group);
                for (int i = 0; i < collection.Count; i++)
                {
                    if (collection[i].Exp < bestExp)
                    {
                        bestExp = collection[i].Exp;
                        best = (group, i);
                    }
                }
            }
            return best;
        }

        /// <summary>A non-Legendary base form one tier above the traded mon's own base form, a different
        /// species, widening outwards while nothing fits.</summary>
        private static PokemonSpeciesDefinitionAsset OfferFor(PokemonInstance yours, PokemonSpeciesLibrary species, int seed)
        {
            var baseForm = ExperienceResolver.BaseFormOf(species.GetById(yours.SpeciesId), species);
            if (baseForm == null)
            {
                return null;
            }

            var evolvedForms = new HashSet<int>(
                species.AllSpecies.Where(s => s != null && s.EvolvesInto != null).Select(s => s.EvolvesInto.Id));
            var candidates = species.AllSpecies
                .Where(s => s != null && !s.IsLegendary && !evolvedForms.Contains(s.Id) && s.Id != baseForm.Id)
                .ToList();

            int target = SpeciesTier.Clamp(baseForm.Tier + 1);
            for (int widen = 0; widen <= SpeciesTier.MaxTier; widen++)
            {
                var pool = candidates.Where(s => Math.Abs(s.Tier - target) == widen).ToList();
                if (pool.Count > 0)
                {
                    return pool[new DeterministicRandom(seed ^ 0x68e31da4).NextInt(pool.Count)];
                }
            }
            return null;
        }

        // ---- Team Rocket Ambush ---------------------------------------------------------------

        private static RoadEvent BuildRocket(RunState state, ItemLibrary items, int seed) => new RoadEvent
        {
            Kind = RoadEventKind.RocketAmbush,
            Title = "Team Rocket Ambush",
            Body = "\"Prepare for trouble!\" Two Rocket Grunts block the path. Their bag of stolen goods sits " +
                   "just out of reach.",
            Seed = seed,
            Choices =
            {
                new RoadEventChoice { Label = $"Pay them off (lose ${Toll(state)})" },
                new RoadEventChoice { Label = "Hand over an item from your bag", Available = state.Items.Count > 0 },
                new RoadEventChoice
                {
                    Label = $"Grab their loot and run ({LootWinPercent}%: an item, or lose 1 Morale)",
                    Available = items != null && items.AllItems.Any(i => i != null),
                },
            },
        };

        private static RoadEventOutcome ResolveRocket(int index, RunState state, ItemLibrary items, DeterministicRandom rng)
        {
            switch (index)
            {
                case 0:
                {
                    int toll = Toll(state);
                    state.Money -= toll;
                    return new RoadEventOutcome { Message = $"The Grunts count out your ${toll} and blast off." };
                }
                case 1:
                {
                    int pick = rng.NextInt(state.Items.Count);
                    string id = state.Items[pick];
                    state.Items.RemoveAt(pick);
                    string name = items?.GetById(id)?.DisplayName ?? "item";
                    return new RoadEventOutcome { Message = $"They snatch your {name} and blast off." };
                }
                default:
                {
                    if (rng.NextInt(100) < LootWinPercent)
                    {
                        var item = RandomItem(items, rng);
                        state.Items.Add(item.Id);
                        return new RoadEventOutcome { Message = $"You get away clean with a {item.DisplayName}! It's in your bag." };
                    }

                    state.Morale--;
                    return new RoadEventOutcome
                    {
                        Message = state.IsRunOver
                            ? "They catch you. The team's morale is broken. The run ends here."
                            : $"They catch you and send you packing empty-handed. Morale {state.Morale} left.",
                    };
                }
            }
        }

        private static ItemDefinitionAsset RandomItem(ItemLibrary items, DeterministicRandom rng)
        {
            var pool = items?.AllItems.Where(i => i != null).ToList();
            return pool == null || pool.Count == 0 ? null : pool[rng.NextInt(pool.Count)];
        }
    }
}
