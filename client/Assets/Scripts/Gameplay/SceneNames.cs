namespace Pets.Gameplay
{
    /// <summary>The build's scene names in one place, so the runtime navigation
    /// (SceneNavigator, CharacterSelectController) and the Editor scene builders that create
    /// those scenes can't drift apart. SceneManager.LoadScene takes these by name, which only
    /// resolves for scenes listed in Build Settings — see SceneBuilderUtils.SetBuildScenes, which
    /// BuildAllScenes keeps in sync with exactly this set.</summary>
    public static class SceneNames
    {
        public const string Home = "Home";
        public const string CharacterSelect = "CharacterSelect";
        public const string Map = "RegionMap";
        public const string IngameMenu = "IngameMenu";
        public const string Team = "Team";
        public const string History = "History";
        public const string Credits = "Credits";

        /// <summary>Dev-only roster screen for stuffing mons into a run by hand (see
        /// DevRosterController). Shipped in the build like the rest — this is a personal,
        /// non-commercial project (PLAN.md §9), so there's no release channel to keep it out
        /// of.</summary>
        public const string DevRoster = "DevRoster";
    }
}
