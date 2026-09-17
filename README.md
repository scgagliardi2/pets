# Pokémon Roguelite Autobattler — React/TypeScript rebuild

A single-player roguelite: assemble a team of Pokémon, watch automatic battles resolve, and build
a team whose *type composition* grants passive bonuses. Slay-the-Spire-shaped meta layer,
Super-Auto-Pets-shaped combat.

This is a rebuild of an existing, playable Unity/C# game. The Unity version remains the reference
implementation for combat behaviour while both exist.

**Personal, non-commercial fan project.** Pokémon species, names and sprites are used under that
understanding. No monetisation, no wide distribution.

---

## Status

**Stages 1-7 of 7 are complete.** 309 tests passing. See "Build order" below.

| | |
|---|---|
| Sim core | done — all 6 shared golden fixtures passing |
| Content | done — 183 species, 20 passives, validated against the Unity assets |
| Battle screen | playable — `npm run dev` |
| Run layer | playable loop — map, fights, EXP, evolution, morale |
| Catching | live — odds, ball tiers, Step-boundary throws |
| Rest of the loop | branching maps, 8 Locations, the Center shop |
| Breadth | traveller map, drag-and-drop, Box screen |

There is **no UI yet**. The sim is exercised through the test suite and the text harness.

---

## Getting started

```bash
npm install
npm test            # the full suite, including the shared golden fixtures
npm run typecheck
npm run fight       # a demo fight, printed Step by Step
```

Useful harness invocations:

```bash
npm run fight -- --scenario midrun    # a party three Locations in: EXP and an evolution
npm run fight -- --scenario drain     # stacked lifesteal, ended by sudden death
npm run fight -- --scenario status    # poison and burn vs a Fairy ward and Steel reduction
npm run fight -- --scenario legends   # a tier-6 mirror
npm run fight -- --scenario swarm     # five Bug-types stacking Swarm Scurry
npm run fight -- --seed 7 --quiet     # a different seed, event log only
```

Scenarios build teams from real species by name, with optional EXP and evolutions
(`'Charmeleon:14:1'`), so editing them is the fastest way to feel out a matchup.

```bash
npm run dev     # the battle screen, at the URL it prints
```

Wild fights carry a throw panel: pick a ball and the odds against the foe's current Lead are shown
before you commit it. Odds rise sharply as the target weakens, so the loop is fight it down, then
throw. Throws resolve only between Steps.

Your traveller sits on the last node taken and slides to the next. The team builder runs along the
bottom: drag mons between slots to reorder, drag them out to the Box, and open the Box for a full
view of everything you own. Balls can be clicked or dragged straight onto the enemy.

Opens on the Location map. Pick a node to fight it; the battle screen opens **paused** with the
synergy opening already applied, so you can read the board before anything moves. Press Play, or
One Step to walk a Step at a time. Reorder your line-up in the panel on the right — position 0
leads, position 1 supports, the rest wait.

Sprites load from PokeAPI over the network. To work offline, copy the Unity repo's
`client/Assets/Art/Pokemon-Sprites/animated_sprites/` into `public/sprites/` and pass
`{ source: 'local' }` to `spriteUrl`.

---

## Layout

```
src/
  sim/              pure TypeScript, zero React, fully unit-tested
    types.ts          BattleState, Combatant, StepEvent, the enums
    config.ts         balance constants (charge threshold, sudden death, caps)
    rng.ts            seeded PRNG
    damage.ts         the one shared damage pipeline
    advanceStep.ts    the Step loop
    synergy.ts        the opening pass — all 18 type synergies
    runners.ts        precomputed and on-demand runners
  content/          the roster and the rules for deriving from it
    species.json      GENERATED - 183 curated species
    passives.json     GENERATED - the 20 authored passives
    index.ts          typed registries, lookups, evolution chains
    statGrowth.ts     what EXP buys
    factory.ts        PokemonInstance -> Combatant
    sprites.ts        sprite URLs and scaling rules
  meta/             run rules, pure
    balls.ts          ball tiers, what each is worth, inventory
    locations.ts      the eight Locations and their themes
    mapGenerator.ts   branching paths, with reachability invariants
    shop.ts           the Pokémon Center
    catching.ts       odds, the roll, what a catch produces
    progression.ts    how difficulty scales
    runState.ts       party, box, morale, money, badges
    experience.ts     EXP, evolution, catch-up
    encounters.ts     who you fight, and the first Location
  state/
    runStore.ts       the one mutable thing
  playback/         animation timing, no React
    clock.ts          pausable, speed-aware virtual clock
    display.ts        board state partway through a Step
    player.ts         drives the sim through the clock
  ui/
    layout.ts         every field coordinate, single source of truth
    theme.css
    MonView.tsx       sprite, charge arc, shield bubble, readout
    BattleScreen.tsx
    RunScreen.tsx     map, team, results
    TraitCounters.tsx
  harness/
    fight.ts          text-mode fight printer
test/
  fixtures/         vendored golden fixtures + PROVENANCE.json
scripts/
  check-fixtures.mjs  fixture drift check against the Unity repo
  build_species.py    regenerates the content JSON from the Unity repo
```

## Regenerating the content

`species.json` and `passives.json` are generated, not hand-authored. Don't edit them:

```bash
python3 scripts/build_species.py /path/to/pets-unity-repo
```

It derives every tier, stat line and growth value from the roster spreadsheet using the same
rules as the Unity importer (`SpeciesTier.cs`), then **validates all 183 against the Unity
species assets on disk** and refuses to write anything if a single number disagrees. That check
is the content-side equivalent of the golden fixtures.

### The one architectural rule

**The simulation is pure TypeScript with zero React in it.** No hooks, no components, no DOM, no
clock. In the Unity version the equivalent separation is what made the combat testable at all, and
every bug that *wasn't* caught early lived in the layer that had engine dependencies.

`src/sim/` must never import React. When the UI arrives, it consumes the event log; it does not
reach into the sim.

---

## The golden fixtures

`test/fixtures/` holds vendored copies of the Unity repo's `shared/fixtures/` — language-agnostic
JSON cases that both implementations run. They are the only artifact that proves the two agree on
how combat actually resolves, rather than each agreeing with its own reading of the spec.

Because they're vendored rather than shared, they can drift. `npm run fixtures:check` guards that:

```bash
npm run fixtures:check              # against the pinned commit
npm run fixtures:check -- --ref main  # against the Unity repo's current head
```

It fails loudly if a local copy was edited *or* if the upstream file changed. The second mode is
the useful one — an upstream change means a battle rule moved and the sim needs reconciling.

`test/fixtures/PROVENANCE.json` records the source commit and a SHA-256 per file. Don't hand-edit
fixtures: change the rule in the Unity repo first, then re-vendor and update the hashes in the
same commit.

---

## Combat, in brief

Two active mons per side, a **Lead** and a **Support**; everyone else is dormant. Combat runs in
discrete **Steps**, and one `advanceStep` function is the whole of it. Beats, in order:

1. **Attack exchange** — both Leads damage each other simultaneously, flat Attack vs Attack.
   Speed is irrelevant here.
2. **Charge accumulation** — every active mon gains its Speed. Paralysed halves it, Asleep zeroes
   it. A mon promoted *this* Step doesn't accrue this Step.
3. **Passive resolution** — anyone at or above the charge threshold fires and resets to 0. The
   trigger set is fixed at the start of the beat, so passives don't cascade within a Step.
   - **3.5 Status ticks** — poison and burn, bypassing shields and reduction.
   - **3.6 Sudden death** — from Step 30, escalating true damage to both Leads.
4. **Faint check and promotion** — the only place removal happens.

Three stats only: Attack, Health, Speed. No Defence — Attack subtracts directly from Health.
Speed drives nothing but charge rate.

Damage always takes one path: **flat reduction → shield absorption → HP**, then lifesteal off the
HP damage actually dealt.

### Things that look optional and are not

- **Sudden death.** Standing modifiers accumulate all battle, so stacked lifesteal past 100% makes
  a fight literally unwinnable by either side. Two Bulbasaurs with Vine Drain reach that on Step
  12. Before sudden death existed, such a fight ground out the 200-Step cap with frozen HP bars,
  which on screen is indistinguishable from a hang. Run `npm run fight -- --scenario drain` to see
  it resolve instead.
- **The minimum damage floor and the lifesteal cap.** Same reason, narrower.
- **The opening being its own beat.** Synergy effects applied *inside* Step 1 are invisible — a
  1-point shield appears and pops in the same frame. They're applied and rendered before Step 1.

---

## The run, in one paragraph

You start with two tier-1 mons and pick your way through a branching Location: an entry choice,
four layers of forks, then the Gym. Some nodes are wild fights, some are a Pokémon Center. Eight
Locations, each with its own type theme and Gym Leader. Every win gives one EXP to everyone who fought; twelve EXP is an evolution. A loss
costs one Morale, and at zero Morale the run ends. **Damage does not carry between fights** —
everyone is restored after every battle, fainted included — so the pressure is Morale, not
attrition, and a run is decided by the team you build rather than the health you nursed.

## Balance findings worth revisiting

Recorded, not fixed — each is a design call rather than a bug, and each has a test pinning the
current behaviour so it changes deliberately.

- **ADR 0014's two-Bulbasaur example no longer stalls.** The ADR justifies sudden death with two
  Vine Drain Bulbasaurs becoming unkillable from Step 12. Under ADR 0009's retuned tier lines they
  resolve in three Steps, because Attack now grows alongside Health. Sudden death is still right —
  six stacked Grass-types reach it reliably — but the canonical example is stale.
- **A fight can legitimately outrun the spec's assumed ceiling.** `battle-sim-spec.md` puts the
  longest real fight at 17 Steps, and `SUDDEN_DEATH_STEP` (30) was chosen against that. A Grass
  stack with EXP runs 37. Sudden death can now decide a fight that was resolving on its own. In a
  4,000-fight random sweep it fired once, so it's rare rather than routine.
- **Catching is what keeps you on the curve, and it is now measured.** Before it existed, 0 of
  200 simulated runs were winnable: the tier cap rises one per badge and a tier is worth far more
  than a Location's EXP — tier 1 spends 8 stat points, tier 2 spends 18, while winning every fight
  in a Location earns 4. With catching, 140 of 150. So catching is load-bearing, not flavour.
- **The shop fixed the strategy gap and left the difficulty problem.** Before it existed, a player
  who threw freely won 93% of runs and one who held out for 65% odds won 42% — the game was mostly
  a test of throw aggression. With the shop, those become 92% and 84%: a cautious player banks
  money and buys better balls, so both styles work. That is the right shape, but the whole band is
  far too high. The run needs a difficulty pass, not another system.
- **Tier buys bigger numbers, not longer fights.** Attack is capped at just under half of whatever
  Speed leaves, so Health only edges ahead by a point or two at any tier: Mewtwo is 27/28 and a
  tier-6 mirror ends as fast as a tier-1 one. EXP growth is what lengthens fights, which is what
  ADR 0009 intended — but it means tier alone doesn't pace a run.

## Two behaviours that surprise people

Both are faithful to the Unity implementation and both have tests pinning them down:

1. **A mon with no passive still emits `PassiveTriggered` and resets its charge** when it crosses
   the threshold. The trigger set is built from charge alone. A charge arc in the UI can't assume
   a passive exists behind it.
2. **The attack exchange emits a `Damage` event of amount 0** for a 0-Attack Lead. The animation
   layer must not treat every `Damage` event as a hit worth flashing.

---

## Known divergence from Unity

**Seed conditioning in `rng.ts`.** Unity seeds xorshift32 directly, and that generator's first
draw is *exactly* linear in the seed — seed N produces N times what seed 1 produces. Run seeds are
exactly the kind of thing that ends up small and consecutive, so this build mixes the seed through
splitmix32 first. The stream algorithm is unchanged.

Nothing in the sim currently draws from the RNG — no probabilistic passive exists yet — so the
divergence is unobservable today. **The moment a probabilistic effect is added, Unity needs the
same conditioning or the two implementations will disagree.** `createLegacyUnityRandom` reproduces
Unity's exact stream for cross-checking.

---

## Build order

Each stage should be playable before moving on.

1. ~~**Sim core.**~~ Types, `advanceStep`, damage pipeline, statuses, charge, sudden death, caps.
2. ~~**Content.**~~ Species JSON with tiers, passives, the synergy table.
3. ~~**Battle screen.**~~ Fixed-aspect field, event-log playback, animation beats, pause/step
   controls. Still to come here: the Pokeball throw control and per-tier catch odds.
4. ~~**Run layer.**~~ Party, box, EXP, evolution, morale, one hand-authored Location.
5. ~~**Catching.**~~ Odds formula, ball tiers and inventory, Step-boundary throws.
6. ~~**The rest of the loop.**~~ Branching map generation, eight Locations, the Center shop.
   Road events are still to come here.
7. ~~**Breadth.**~~ Traveller token on the map, drag-and-drop throwing and roster, the Box
   screen. Still to come: road events, meta-progression between runs, and a difficulty pass.

---

## Source material

The Unity repo carries the design documents this implementation follows:

- `docs/battle-sim-spec.md` — the Step loop, specified formally
- `docs/content-schema.md` — the data shapes
- `docs/architecture-decisions/` — 16 ADRs recording *why* each rule is the way it is
- `docs/pokemon_stats_unique.xlsx`, `roster_evolution_chains.json`, `roster_pokeapi_ids.json`
- `client/Assets/Art/Pokemon-Sprites/animated_sprites/` — 185 front + 185 back animated GIFs

**Note on `battle-sim-spec.md` §12:** it still states that a point of EXP adds +1 Attack *and*
Health, citing ADR 0008. ADR 0009 supersedes that — a point buys Attack *or* Health, weighted per
species — and the lockstep growth §12 describes is the specific bug ADR 0009 was written to fix.
Follow ADR 0009.
