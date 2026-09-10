namespace Pets.Gameplay
{
    /// <summary>One completed run's summary, persisted by SaveSystem.AppendHistory/LoadHistory.</summary>
    public sealed class RunHistoryEntry
    {
        public string CompletedAtUtc;
        public int RoundReached;
        public bool Victory;
    }
}
