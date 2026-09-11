using UnityEngine;
using Pets.Data;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Scene entry point for a run (design doc §3): creates the RunState with a starting
    /// Lead/Support pair and the Phase 0 Forest node-map, then hands off to LocationFlowController.
    /// Full character creation (starter choice, 3 secondary options) is Phase 1 — this always
    /// starts the same fixed pair for now.</summary>
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
            // Phase 0 is a single scene with no cross-scene transitions yet, so this is a plain
            // same-scene accessor rather than a DontDestroyOnLoad persistent singleton — that
            // would otherwise survive a scene reload and block the new scene's own bootstrapper
            // from ever initializing (a real risk once a "new run" flow reloads this scene).
            Instance = this;
            State = BuildNewRun();
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
            var lead = starterLead != null ? starterLead : speciesLibrary.AllSpecies[0];
            var support = starterSupport != null ? starterSupport : speciesLibrary.AllSpecies[1];

            var state = new RunState
            {
                RunSeed = System.Environment.TickCount,
                Nodes = ForestLocationFactory.BuildNodes(),
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
