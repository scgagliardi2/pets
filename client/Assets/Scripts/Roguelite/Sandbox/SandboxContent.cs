using Pets.Roguelite.Simulation;

namespace Pets.Roguelite.Sandbox
{
    /// <summary>
    /// A small, hand-picked slice of the real roster (base stats copied from
    /// docs/pokemon_stats_unique.xlsx, not invented) with a hand-authored passive per species —
    /// one deliberately drawn from each of the Type-flavor seeds in the design doc's §11, so the
    /// sandbox exercises a spread of the effect vocabulary out of the box.
    ///
    /// This is a hand-typed stand-in for the real content pipeline (PLAN.md §8 — xlsx + PokeAPI ->
    /// PokemonSpeciesDefinition ScriptableObject assets, resolved through a PassiveLibrary per
    /// docs/content-schema.md §11). Nothing here is meant to be the final numbers or the final
    /// authoring path — it exists so you can run real battles and add new species/passives by
    /// editing one file, before that pipeline is built.
    ///
    /// To try a new Pokémon or ability: add a stats row below (matching
    /// docs/pokemon_stats_unique.xlsx), write a PassiveDefinition using EffectDefinition's static
    /// helpers, then add a factory method that calls <see cref="NewMon"/>. To try a new matchup:
    /// call two factory methods and pass them to <see cref="NewBattle"/> — see
    /// BattleSandboxTests.cs for examples.
    /// </summary>
    public static class SandboxContent
    {
        // ---- Passives -----------------------------------------------------------------------
        // One per Type-flavor seed exercised here (design doc §11). Numbers are placeholders —
        // see PLAN.md §10 on balance being an ongoing tuning effort, not a one-time task.

        public static readonly PassiveDefinition Ember = new PassiveDefinition(
            "ember-burst", "Ember Burst", PokemonType.Fire,
            EffectDefinition.ApplyStatus(TargetSelector.EnemyLead, StatusType.Burned));

        public static readonly PassiveDefinition AquaShield = new PassiveDefinition(
            "aqua-shield", "Aqua Shield", PokemonType.Water,
            EffectDefinition.Shield(TargetSelector.Self, 10));

        public static readonly PassiveDefinition LeechBite = new PassiveDefinition(
            "leech-bite", "Leech Bite", PokemonType.Grass,
            EffectDefinition.DealDamage(TargetSelector.EnemyLead, 8),
            EffectDefinition.Heal(TargetSelector.Self, 8));

        public static readonly PassiveDefinition StaticShock = new PassiveDefinition(
            "static-shock", "Static Shock", PokemonType.Electric,
            EffectDefinition.ApplyStatus(TargetSelector.EnemyLead, StatusType.Paralyzed));

        public static readonly PassiveDefinition StoneWall = new PassiveDefinition(
            "stone-wall", "Stone Wall", PokemonType.Rock,
            EffectDefinition.Shield(TargetSelector.Self, 15));

        public static readonly PassiveDefinition SpiritDrain = new PassiveDefinition(
            "spirit-drain", "Spirit Drain", PokemonType.Ghost,
            EffectDefinition.DealDamage(TargetSelector.EnemyLead, 6),
            EffectDefinition.Heal(TargetSelector.Self, 4));

        // ---- Species (base-stage stats from docs/pokemon_stats_unique.xlsx) -------------------

        public static BattleMon Charmander() =>
            NewMon("Charmander", PokemonType.Fire, null, attack: 52, health: 39, speed: 65, passive: Ember);

        public static BattleMon Squirtle() =>
            NewMon("Squirtle", PokemonType.Water, null, attack: 48, health: 44, speed: 43, passive: AquaShield);

        public static BattleMon Bulbasaur() =>
            NewMon("Bulbasaur", PokemonType.Grass, PokemonType.Poison, attack: 49, health: 45, speed: 45, passive: LeechBite);

        public static BattleMon Pikachu() =>
            NewMon("Pikachu", PokemonType.Electric, null, attack: 55, health: 35, speed: 90, passive: StaticShock);

        public static BattleMon Geodude() =>
            NewMon("Geodude", PokemonType.Rock, PokemonType.Ground, attack: 80, health: 40, speed: 20, passive: StoneWall);

        public static BattleMon Gastly() =>
            NewMon("Gastly", PokemonType.Ghost, PokemonType.Poison, attack: 35, health: 30, speed: 80, passive: SpiritDrain);

        // ---- Helpers --------------------------------------------------------------------------

        private static int instanceCounter;

        /// <summary>
        /// Builds a fresh PokemonInstance + BattleMon in one step. Each call produces a brand-new
        /// object (battles mutate BattleMon in place), so calling e.g. Charmander() twice gives
        /// you two independent mons — safe to use the same factory method on both sides of a
        /// mirror match.
        /// </summary>
        public static BattleMon NewMon(string name, PokemonType type1, PokemonType? type2, int attack, int health, int speed, PassiveDefinition passive)
        {
            var instance = new PokemonInstance
            {
                InstanceId = $"{name}-{instanceCounter++}",
                DisplayName = name,
                Type1 = type1,
                Type2 = type2,
                CurrentStats = new Stats(attack, health, speed),
                CurrentHP = health,
            };
            return BattleMon.Create(instance, passive);
        }

        /// <summary>Builds a ready-to-run BattleState from two ordered line-ups (index 0 = Lead).</summary>
        public static BattleState NewBattle(BattleMon[] lineUpA, BattleMon[] lineUpB, int seed = 1) =>
            new BattleState(new BattleSide(lineUpA), new BattleSide(lineUpB), seed);
    }
}
