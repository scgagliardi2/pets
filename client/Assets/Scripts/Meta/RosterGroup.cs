namespace Pets.Meta
{
    /// <summary>Which of a run's two mon collections a slot belongs to — the active line-up or the
    /// Box (design doc §7). Exists so RunState.MoveMon can be given a from/to pair instead of four
    /// overloads, and so the Team screen's slots can say what they are without each knowing which
    /// List they point at.</summary>
    public enum RosterGroup
    {
        Party,
        Box
    }
}
