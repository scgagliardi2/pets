using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Growth at the scale of a *run* rather than a mon (ADR 0006): paying the line-up for
    /// a node, and keeping every mon the run owns at or above the run's floor level.
    ///
    /// The floor is the fairness rule the rest of the model hangs off. A run tracks the EXP it has
    /// paid out (RunState.RunExp) and holds everything it owns at one level below what that buys
    /// (RunState.FloorLevel). So:
    /// - a mon caught or adopted in Location 5 is playable the moment you get it, instead of
    ///   arriving five Locations of growth behind and going straight in the Box forever — which is
    ///   what made catching pointless after the first Location;
    /// - a mon sat in the Box doesn't rot while the line-up fights, so rotating the team is free;
    /// - personal EXP still means something, because it is the only thing that puts a mon *above*
    ///   the floor — and since every mon in the line-up is paid equally, combining duplicates is
    ///   the lever a player actually pulls to get there.
    ///
    /// Separate from RunState because all of this needs the species library to recompute stats and
    /// follow evolutions, and RunState is deliberately library-free.</summary>
    public static class RunProgression
    {
        /// <summary>Pays every mon in the active line-up, banks the same amount as run progress, and
        /// pulls the whole roster up to the resulting floor. Returns every evolution it set off,
        /// line-up and Box alike, so the caller can tell the player about them.</summary>
        public static List<ExperienceResolver.Evolution> GrantToLineUp(
            RunState state, int amount, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<ExperienceResolver.Evolution>();
            if (state == null || amount <= 0)
            {
                return evolutions;
            }

            state.RunExp += amount;
            foreach (var mon in state.LineUp)
            {
                evolutions.AddRange(ExperienceResolver.GrantExp(mon, amount, library));
            }

            evolutions.AddRange(ApplyFloor(state, library));
            return evolutions;
        }

        /// <summary>Brings a mon that has just joined the run — caught, adopted, handed over by an
        /// Event — up to the run's floor, evolving it as far as that level takes it. Call this
        /// *before* putting it in the Box or line-up so it's never briefly on screen at level 1.</summary>
        public static List<ExperienceResolver.Evolution> Onboard(
            RunState state, PokemonInstance mon, PokemonSpeciesLibrary library) =>
            state == null
                ? new List<ExperienceResolver.Evolution>()
                : ExperienceResolver.SetMinLevel(mon, state.FloorLevel, library);

        /// <summary>Raises every mon the run owns to the current floor. Idempotent — a mon already
        /// at or above it is untouched — so it's safe to call after anything that might have moved
        /// the floor.</summary>
        public static List<ExperienceResolver.Evolution> ApplyFloor(RunState state, PokemonSpeciesLibrary library)
        {
            var evolutions = new List<ExperienceResolver.Evolution>();
            if (state == null)
            {
                return evolutions;
            }

            int floor = state.FloorLevel;
            foreach (var mon in state.LineUp)
            {
                evolutions.AddRange(ExperienceResolver.SetMinLevel(mon, floor, library));
            }
            foreach (var mon in state.Box)
            {
                evolutions.AddRange(ExperienceResolver.SetMinLevel(mon, floor, library));
            }
            return evolutions;
        }
    }
}
