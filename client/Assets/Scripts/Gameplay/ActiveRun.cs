using Pets.Data;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>The run currently being played, held across scene loads. The screens that make up
    /// a run are separate scenes now (Map, Ingame Menu, Team), and RunState is plain C# owned by a
    /// scene object, so without a holder like this it would be rebuilt from scratch every time the
    /// player opened the menu.
    ///
    /// Static rather than a DontDestroyOnLoad singleton, for the same reason PendingRunSelection
    /// is: there's no save system yet, and plain statics can't accidentally survive as a duplicate
    /// scene object. Written only by RunBootstrapper (Begin) and the Home screen (End); everything
    /// else reads it. A future local-save pass replaces this with a real load/store.</summary>
    public static class ActiveRun
    {
        public static RunState State { get; private set; }
        public static PokemonSpeciesLibrary Library { get; private set; }

        public static bool HasRun => State != null;

        public static void Begin(RunState state, PokemonSpeciesLibrary library)
        {
            State = state;
            Library = library;
        }

        public static void End()
        {
            State = null;
            Library = null;
        }
    }
}
