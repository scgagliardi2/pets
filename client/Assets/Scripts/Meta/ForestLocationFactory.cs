using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>The one hand-authored Location (PLAN.md §6, Phase 0): a Forest, biased toward
    /// Grass/Bug/Flying wild encounters (design doc §4 table). Its node-map is now the generated
    /// branching graph (RegionMapGenerator/RegionMapTraversal) rather than a bespoke sequence —
    /// this factory only supplies the type bias PvE nodes draw wild encounters from
    /// (EncounterGenerator). The Gym node draws from the whole roster instead (GymTeamGenerator),
    /// since a Gym Leader's team isn't meant to read as "more Forest wildlife."</summary>
    public static class ForestLocationFactory
    {
        public static readonly PokemonType[] TypeBias = { PokemonType.Grass, PokemonType.Bug, PokemonType.Flying };
    }
}
