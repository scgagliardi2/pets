using UnityEngine;
using Pets.Data;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Scene entry point for a run (design doc §3): creates the RunState with a starting
    /// Lead/Support pair and the Phase 0 node-map, and publishes it to ActiveRun so the run's other
    /// scenes (Ingame Menu, Team) see the same one. Prefers a pair chosen in CharacterSelect
    /// (PendingRunSelection) if one is waiting; falls back to the inspector-configured pair (or the
    /// library's first two species) so the scene still works standalone, e.g. when testing it
    /// directly without going through Character Select.</summary>
    public sealed class RunBootstrapper : MonoBehaviour
    {
        public static RunBootstrapper Instance { get; private set; }

        [SerializeField] private PokemonSpeciesLibrary speciesLibrary;
        [SerializeField] private PokemonSpeciesDefinitionAsset starterLead;
        [SerializeField] private PokemonSpeciesDefinitionAsset starterSupport;

        public PokemonSpeciesLibrary SpeciesLibrary => speciesLibrary;
        public RunState State { get; private set; }

        private void Awake()
        {
            // A plain same-scene accessor rather than a DontDestroyOnLoad persistent singleton —
            // that would otherwise survive a scene reload and block the new scene's own
            // bootstrapper from ever initializing. Cross-scene continuity is ActiveRun's job
            // instead: re-entering this scene from the Ingame Menu must resume the run in
            // progress, not silently reroll the player's line-up, so an existing ActiveRun always
            // wins over building a fresh one. Home's "New Game" is what clears it.
            Instance = this;
            if (ActiveRun.HasRun)
            {
                State = ActiveRun.State;
                return;
            }
            State = BuildNewRun();
            ActiveRun.Begin(State, speciesLibrary);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private RunState BuildNewRun()
        {
            PokemonSpeciesDefinitionAsset lead, support;
            if (PendingRunSelection.HasSelection)
            {
                lead = PendingRunSelection.Lead;
                support = PendingRunSelection.Support;
                PendingRunSelection.Clear();
            }
            else
            {
                lead = starterLead != null ? starterLead : speciesLibrary.AllSpecies[0];
                support = starterSupport != null ? starterSupport : speciesLibrary.AllSpecies[1];
            }

            var state = new RunState
            {
                RunSeed = System.Environment.TickCount,
                LineUp =
                {
                    PokemonInstanceFactory.Create(lead, "player-lead"),
                    PokemonInstanceFactory.Create(support, "player-support")
                }
            };
            return state;
        }
    }
}
