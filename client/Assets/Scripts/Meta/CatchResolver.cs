using System.Collections.Generic;
using Pets.Data;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Stubbed catching (PLAN.md §6, Phase 0): "pick 1 from defeated" rather than the
    /// full drag-a-Pokéball Step-boundary throw system (design doc §12.1, Phase 1).</summary>
    public static class CatchResolver
    {
        /// <summary>The wild mons that fainted during the fight, as run-level instances.
        ///
        /// Taken from the log's Faint events rather than by testing the line-up's own HP: a battle
        /// runs on copies (Pets.Simulation.BattleCombatant), so the instances handed to the runner
        /// come back undamaged and "which of these is dead" is no longer a question the roster can
        /// answer. The events are the record of what happened, which is the right thing to ask.</summary>
        public static List<PokemonInstance> GetDefeated(
            IReadOnlyList<PokemonInstance> wildLineUp, StepLog log, Side wildSide) =>
            GetDefeated(wildLineUp, log.Events, wildSide);

        /// <summary>Same as the StepLog overload, against a raw event list — for a caller (the
        /// Battle screen) that accumulates a fight's events Step by Step rather than holding a
        /// full StepLog.</summary>
        public static List<PokemonInstance> GetDefeated(
            IReadOnlyList<PokemonInstance> wildLineUp, IEnumerable<StepEvent> events, Side wildSide)
        {
            var faintedIds = new HashSet<string>();
            foreach (var evt in events)
            {
                if (evt.Kind == StepEventKind.Faint && evt.SourceSide == wildSide)
                {
                    faintedIds.Add(evt.SourceInstanceId);
                }
            }

            var defeated = new List<PokemonInstance>();
            foreach (var mon in wildLineUp)
            {
                if (faintedIds.Contains(mon.InstanceId))
                {
                    defeated.Add(mon);
                }
            }
            return defeated;
        }

        /// <summary>Adds a fresh, full-health copy of the defeated mon's species to the Box.</summary>
        public static void Catch(RunState state, PokemonInstance defeated, PokemonSpeciesLibrary library)
        {
            var species = library.GetById(defeated.SpeciesId);
            if (species == null)
            {
                return;
            }
            string instanceId = $"box-{species.Id}-{state.Box.Count}";
            state.Box.Add(PokemonInstanceFactory.Create(species, instanceId));
        }
    }
}
