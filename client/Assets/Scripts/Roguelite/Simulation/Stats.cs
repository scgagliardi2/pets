namespace Pets.Roguelite.Simulation
{
    /// <summary>
    /// Attack/Health/Speed triple. Mirrors PokemonSpecies.baseStats /
    /// PokemonInstance.currentStats in docs/pokemon-roguelite-autobattler-design-doc.md §9.
    /// A level-scaling formula from base stats is still an open question (design doc §20 doesn't
    /// cover it, and PLAN.md §8 notes evolution-stage stats are explicit placeholders per stage
    /// rather than derived) — until one is decided, PokemonInstance.CurrentStats is just set
    /// directly from the species' base stats for its current evolution stage.
    /// </summary>
    public struct Stats
    {
        public int Attack;
        public int Health;
        public int Speed;

        public Stats(int attack, int health, int speed)
        {
            Attack = attack;
            Health = health;
            Speed = speed;
        }
    }
}
