# Battle Sandbox

A lightweight place to try Pokémon, teams, and abilities against each other in the new
Lead/Support/Step combat model (`docs/battle-sim-spec.md`), without needing the real content
pipeline, ScriptableObject assets, or any UI to exist yet. This is what PLAN.md §6 Phase 0 calls
"the battle-sim rework" made runnable and pokeable.

## Running it

Open Unity's Test Runner window (Window → General → Test Runner), EditMode tab, and run
`Pets.Roguelite.Tests`. Each test prints a full Step-by-step transcript via `Debug.Log` /
`TestContext.Progress.WriteLine` — click a passed/failed test in the Test Runner to see its
transcript in the log, or check the Console window while it runs.

You don't need a scene, a build, or Play mode for any of this — it's all EditMode.

## Trying a different matchup with existing Pokémon

Open `BattleSandboxTests.cs` and either edit an existing `[Test]` method or add a new one:

```csharp
[Test]
public void MyMatchup()
{
    var a = SandboxContent.NewBattle(
        new[] { SandboxContent.Pikachu(), SandboxContent.Geodude() },
        new[] { SandboxContent.Gastly(), SandboxContent.Squirtle() },
        seed: 42);

    var (log, outcome) = PrecomputedStepLogRunner.Run(a);
    TestContext.Progress.WriteLine(BattleTranscript.Render(log));
    Assert.That(outcome, Is.Not.EqualTo(BattleOutcome.Draw)); // or whatever you're checking
}
```

The first mon in each array is the Lead, the second is the Support — add more for a bench (they
sit dormant until something in front of them faints).

## Adding a new Pokémon

Open `SandboxContent.cs`:
1. Look up the species' base-stage stats in `docs/pokemon_stats_unique.xlsx` (Attack/HP/Speed,
   Type 1/2) — don't invent numbers, the sheet is the source of truth for the curated roster.
2. Add a factory method following the existing pattern (`Charmander()`, `Squirtle()`, etc.),
   calling `NewMon(...)`.
3. Give it a passive (see below) or pass `null` if you want to test a mon with no ability yet —
   most of the real 183-species sheet has a blank Ability column today, so this is the common
   case, not an edge case.

## Adding a new passive / trying a new ability idea

Also in `SandboxContent.cs` — declare a `PassiveDefinition` using `EffectDefinition`'s static
helpers:

```csharp
public static readonly PassiveDefinition MyIdea = new PassiveDefinition(
    "my-idea", "My Idea", PokemonType.Fairy,
    EffectDefinition.ClearStatus(TargetSelector.Ally),
    EffectDefinition.Heal(TargetSelector.Ally, 10));
```

Available `TargetSelector` values: `Self`, `Ally` (the other active mon on your own side),
`EnemyLead`, `EnemySupport`. Available `EffectType`s (see `docs/content-schema.md` §4 for the
full rationale of each): `DealDamage`, `Heal`, `Shield`, `ApplyStatus`, `ClearStatus`,
`BuffAttack`, `BuffSpeed`, `DamageReduction`, `Lifesteal`, `ModifyChargeRate`.

If an ability idea genuinely can't be expressed with the current effect vocabulary, that's a
signal to add a new `EffectType` (in `Pets.Roguelite.Simulation`) and handle it in
`BattleSimulator.ApplyEffect` — not to hardcode a one-off for a single Pokémon (CLAUDE.md's "data,
not code" rule applies here too).

## What this sandbox deliberately doesn't do yet

So you don't mistake a gap here for a design decision:

- **No magnitudeByStage scaling** (the "same passive, bigger numbers per evolution stage" rule
  from `docs/content-schema.md` §3) — every passive fires at one fixed magnitude regardless of
  the mon's level or evolution stage, since there's no evolution system here yet either.
- **No team-synergy bonuses** (design doc §11) — those are meant to be computed once at line-up
  assembly, not inside the Step loop, and there's no line-up-assembly step in the sandbox. If you
  want to test a synergy idea, apply the stat bonus by hand to a `BattleMon` right after building
  it, before running the battle.
- **No catching, no Trailblazer, no node-map, no meta-layer at all** — this sandbox is the battle
  simulator only, exactly as scoped in `docs/battle-sim-spec.md` §1.
- **No real content pipeline** — species/passives here are hand-typed, not imported from the xlsx
  or PokeAPI. That import pipeline is separate, later work (PLAN.md §8).
- **Same-Step multi-passive-trigger ordering is still the proposed default**, not a confirmed
  design decision (`docs/battle-sim-spec.md` §6, design doc §20). If you're testing a scenario
  where two mons charge up in the same Step and the order matters, know that the current tie-break
  (Leads before Supports, then higher Speed, then side A before B) is a guess pending
  confirmation, not settled fact.
