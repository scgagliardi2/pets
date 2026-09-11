using Pets.Data;

namespace Pets.Gameplay
{
    /// <summary>Scene-to-scene handoff for the player's Character Select choices (design doc §3).
    /// Plain static state is enough for Phase 1's single hand-off (CharacterSelect -&gt; Game) — no
    /// DontDestroyOnLoad object or save system exists yet. RunBootstrapper reads and clears this
    /// on the next scene's Awake.</summary>
    public static class PendingRunSelection
    {
        public static PokemonSpeciesDefinitionAsset Lead;
        public static PokemonSpeciesDefinitionAsset Support;

        public static bool HasSelection => Lead != null && Support != null;

        public static void Clear()
        {
            Lead = null;
            Support = null;
        }
    }
}
