using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Putting items on mons and taking them off again (design doc §13). A mon holds at most
    /// one; an item is always somewhere — in the run's bag (RunState.Items) or on exactly one mon — so
    /// every operation here moves one rather than creating or destroying it.
    ///
    /// The item's modifiers are copied onto the mon (PokemonInstance.HeldItemStats) and the mon's stats
    /// recomputed, because CurrentStats is derived: adding +3 Attack to it directly would last only
    /// until the next EXP grant.</summary>
    public static class HeldItems
    {
        /// <summary>Takes one <paramref name="item"/> out of the bag and gives it to
        /// <paramref name="mon"/>. Whatever the mon was holding goes back into the bag. False, changing
        /// nothing, if the bag has none.</summary>
        public static bool Equip(RunState state, PokemonInstance mon, ItemDefinitionAsset item,
            PokemonSpeciesLibrary library)
        {
            if (state == null || mon == null || item == null || !state.Items.Remove(item.Id))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mon.HeldItemId))
            {
                state.Items.Add(mon.HeldItemId);
            }
            Set(mon, item.Id, item.StatModifiers, library);
            return true;
        }

        /// <summary>Takes <paramref name="mon"/>'s item off and puts it back in the bag.</summary>
        public static bool Unequip(RunState state, PokemonInstance mon, PokemonSpeciesLibrary library)
        {
            if (state == null || mon == null || string.IsNullOrEmpty(mon.HeldItemId))
            {
                return false;
            }

            state.Items.Add(mon.HeldItemId);
            Set(mon, null, default, library);
            return true;
        }

        /// <summary>Hands <paramref name="from"/>'s item to <paramref name="to"/>; if
        /// <paramref name="to"/> was already holding one, the two mons trade.</summary>
        public static bool Move(PokemonInstance from, PokemonInstance to, PokemonSpeciesLibrary library)
        {
            if (from == null || to == null || from == to || string.IsNullOrEmpty(from.HeldItemId))
            {
                return false;
            }

            string toId = to.HeldItemId;
            var toStats = to.HeldItemStats;
            Set(to, from.HeldItemId, from.HeldItemStats, library);
            Set(from, toId, toStats, library);
            return true;
        }

        /// <summary>Puts a mon's item back in the bag without recomputing it — for a mon that is about
        /// to leave the run (a release, or the duplicate a combine consumes), whose item shouldn't
        /// leave with it.</summary>
        public static void ReturnToBag(RunState state, PokemonInstance mon)
        {
            if (state == null || mon == null || string.IsNullOrEmpty(mon.HeldItemId))
            {
                return;
            }
            state.Items.Add(mon.HeldItemId);
            mon.HeldItemId = null;
            mon.HeldItemStats = default;
        }

        private static void Set(PokemonInstance mon, string itemId, Stats modifiers, PokemonSpeciesLibrary library)
        {
            mon.HeldItemId = string.IsNullOrEmpty(itemId) ? null : itemId;
            mon.HeldItemStats = mon.HeldItemId == null ? default : modifiers;
            ExperienceResolver.Recompute(mon, library);
        }
    }
}
