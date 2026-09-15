using System;
using System.Collections.Generic;
using System.Linq;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>What one visit to a Pokémon Center has for sale: the Pokémon on offer, generated once
    /// when the player walks onto the node and kept on RunState so leaving for the Team screen and
    /// coming back finds the same shelf. An adopted Pokémon's entry becomes null — sold out.</summary>
    public sealed class PokemonCenterStock
    {
        /// <summary>The map node this stock belongs to. A different Center is a different shelf.</summary>
        public string NodeId;

        public List<PokemonInstance> Pokemon = new List<PokemonInstance>();
    }

    /// <summary>The Pokémon Center as a shop (design doc §12.2, §13; ADR 0013): balls of every tier, items, and
    /// Pokémon to adopt, all for money. It replaced Camp's rest — the Center no longer grants EXP or a
    /// next-fight Attack buff; what makes a team stronger here is what the player chooses to buy.
    ///
    /// **Balls and items never run out**; the price is the only limit. **Each Pokémon on offer
    /// can be adopted once.**
    ///
    /// **The Pokémon on offer match the party.** They are drawn from the tiers the line-up's own mons
    /// started at (a species' tier *is* its stat total — Pets.Data.SpeciesTier) and carry the
    /// line-up's average EXP, so what's on the shelf is about as strong as what the player already
    /// fields — a sideways choice of typing and growth, never a shortcut past the run's difficulty.
    /// Like the wild pool (EncounterPool) they are base forms only and never Legendary.</summary>
    public static class PokemonCenterShop
    {
        public const int PokemonOnOffer = 3;
        public const int PokemonPrice = 10;

        public enum Purchase
        {
            Bought,
            NotEnoughMoney,
            SoldOut,
            Unavailable,
        }

        /// <summary>The stock for the Center at <paramref name="nodeId"/>: the run's current one if it
        /// was already opened there, otherwise a fresh shelf rolled from <paramref name="seed"/>.</summary>
        public static PokemonCenterStock OpenFor(RunState state, string nodeId, int seed, PokemonSpeciesLibrary library)
        {
            if (state.CenterStock != null && state.CenterStock.NodeId == nodeId)
            {
                return state.CenterStock;
            }

            state.CenterStock = new PokemonCenterStock
            {
                NodeId = nodeId,
                Pokemon = GenerateOffers(state, nodeId, seed, library),
            };
            return state.CenterStock;
        }

        public static List<PokemonInstance> GenerateOffers(RunState state, string nodeId, int seed,
            PokemonSpeciesLibrary library)
        {
            var offers = new List<PokemonInstance>(PokemonOnOffer);
            if (library == null)
            {
                return offers;
            }

            var pool = PoolFor(state, library);
            int exp = OfferExp(state);
            var rng = new DeterministicRandom(seed);
            for (int i = 0; i < PokemonOnOffer && pool.Count > 0; i++)
            {
                int pick = rng.NextInt(pool.Count);
                var species = pool[pick];
                pool.RemoveAt(pick);
                offers.Add(ExperienceResolver.CreateAtExp(species,
                    $"center-{state.BadgeCount}-{nodeId}-{i}", exp, library));
            }
            return offers;
        }

        /// <summary>The line-up's average EXP, rounded — what every Pokémon on offer carries.</summary>
        public static int OfferExp(RunState state)
        {
            if (state.LineUp.Count == 0)
            {
                return 0;
            }
            return (int)Math.Round(state.LineUp.Average(m => m.Exp), MidpointRounding.AwayFromZero);
        }

        /// <summary>The lowest and highest tier the line-up's mons *started* at — base forms, since a
        /// mon's stats grow off its base form's tier line however far it has evolved (ADR 0009).</summary>
        public static (int Min, int Max) PartyTierRange(RunState state, PokemonSpeciesLibrary library)
        {
            var tiers = state.LineUp
                .Select(m => ExperienceResolver.BaseFormOf(library.GetById(m.SpeciesId), library))
                .Where(s => s != null)
                .Select(s => s.Tier)
                .ToList();
            return tiers.Count == 0 ? (SpeciesTier.MinTier, SpeciesTier.MinTier) : (tiers.Min(), tiers.Max());
        }

        /// <summary>Species eligible to be offered: non-Legendary base forms in the party's tier range,
        /// widened a tier at a time on both sides while there are too few to fill the shelf.</summary>
        public static List<PokemonSpeciesDefinitionAsset> PoolFor(RunState state, PokemonSpeciesLibrary library)
        {
            var evolvedForms = new HashSet<int>(
                library.AllSpecies.Where(s => s != null && s.EvolvesInto != null).Select(s => s.EvolvesInto.Id));
            var candidates = library.AllSpecies
                .Where(s => s != null && !s.IsLegendary && !evolvedForms.Contains(s.Id))
                .ToList();

            var (min, max) = PartyTierRange(state, library);
            var pool = new List<PokemonSpeciesDefinitionAsset>();
            for (int widen = 0; widen <= SpeciesTier.MaxTier; widen++)
            {
                int lo = min - widen;
                int hi = max + widen;
                pool = candidates.Where(s => s.Tier >= lo && s.Tier <= hi).ToList();
                if (pool.Count >= PokemonOnOffer)
                {
                    break;
                }
            }
            return pool;
        }

        /// <summary>What a ball of <paramref name="tier"/> costs — rising with its odds and with how much
        /// of a catch's EXP it keeps (BallCatalog).</summary>
        public static int BallPrice(BallTier tier)
        {
            switch (tier)
            {
                case BallTier.Great: return 5;
                case BallTier.Ultra: return 9;
                default: return 2;
            }
        }

        /// <summary>One ball into the run's BallInventory — the same inventory the battle screen's tray
        /// throws from (ADR 0012).</summary>
        public static Purchase BuyBall(RunState state, BallTier tier)
        {
            if (!Spend(state, BallPrice(tier)))
            {
                return Purchase.NotEnoughMoney;
            }
            state.Balls.Add(tier, 1);
            return Purchase.Bought;
        }

        public static Purchase BuyItem(RunState state, ItemDefinitionAsset item)
        {
            if (item == null)
            {
                return Purchase.Unavailable;
            }
            if (!Spend(state, item.Price))
            {
                return Purchase.NotEnoughMoney;
            }
            state.Items.Add(item.Id);
            return Purchase.Bought;
        }

        /// <summary>Adopts the Pokémon at <paramref name="index"/> of the current stock into the Box —
        /// where a catch lands too, so the Team screen is where it's fielded either way.</summary>
        public static Purchase BuyPokemon(RunState state, int index)
        {
            var stock = state.CenterStock;
            if (stock == null || index < 0 || index >= stock.Pokemon.Count)
            {
                return Purchase.Unavailable;
            }
            var mon = stock.Pokemon[index];
            if (mon == null)
            {
                return Purchase.SoldOut;
            }
            if (!Spend(state, PokemonPrice))
            {
                return Purchase.NotEnoughMoney;
            }

            stock.Pokemon[index] = null;
            state.Box.Add(mon);
            return Purchase.Bought;
        }

        private static bool Spend(RunState state, int price)
        {
            if (state == null || price < 0 || state.Money < price)
            {
                return false;
            }
            state.Money -= price;
            return true;
        }
    }
}
