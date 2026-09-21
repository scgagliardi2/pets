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

**Stages 1-7 of 7 are complete,** plus region art, encounters and player-chosen growth. 414 tests passing. See "Build order" below.

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
    species.json      GENERATED - 183 curated species, with Special
    passives.json     GENERATED - the 20 authored passives
    index.ts          typed registries, lookups, evolution chains
    statGrowth.ts     what EXP buys
    factory.ts        PokemonInstance -> Combatant
    sprites.ts        sprite URLs and scaling rules
  meta/             run rules, pure
    encountersEvents.ts  the road-encounter catalogue
    encounterEffects.ts  what a choice does to a run
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

Four stats: Attack, Health, Speed and **Special**. No Defence — Attack subtracts directly from
Health. Speed drives nothing but charge rate.

Attack lands every Step. **Special is what the ability is worth when the charge bar fills**, and
the default ability is simply "deal damage equal to your Special". An authored ability that
shields, heals or inflicts a status *replaces* that rather than adding to it — so a mon either
hits for its Special or does something else with it, never both. Special is derived from the
species' real Special Attack, scaled onto its tier line and clamped between half and twice its
Attack, and it holds that ratio to Attack as the mon grows.

**Every mon's Health is tripled** (`HEALTH_MULTIPLIER`), applied to the derived value rather than
baked into `species.json`, so the generated roster still validates against the Unity assets number
for number. Typical fights went from six or seven Steps to eleven to twenty.

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

## Spending EXP

**EXP means progress toward evolving, and nothing else.** Spending power is a separate counter,
**stat points**. The two used to be one number, which made every screen ambiguous about what it
was showing.

- **Winning a battle** gives +1 EXP toward evolving, and **+1 to each of the two stats that
  battle node advertised** — applied directly, not as points to assign. The node shows them before
  you take it, so the strategy is in choosing the route rather than clicking the same four buttons
  after every fight.
- **Evolving** plays the flicker-between-forms popup from the handheld games and grants 10 stat
  points to assign by hand. That is now the only routine source of
  them, so the assignment screen is a milestone rather than constant upkeep.
- **Combining** gives +4 EXP toward evolving. Three sacrifices evolve a base-form mon — four
  Charmanders make one Charmeleon.

A stat point buys exactly one stat: a point into Attack moves Attack and nothing else.

Click any card in the line-up (or the Box) to open its full detail panel: bigger stats, its
ability's real description, spending pending points there instead of only after a fight, and
**combining it with another Pokémon in the same evolution line** — a Charmander can absorb a
Charmeleon, not just another Charmander. Combining is a rare-candy, not a pooling of two
histories: the one consumed contributes a single flat `EXP_PER_EVOLUTION`, not its own lifetime
total, so a pile of low-tier catches can't be cashed in as a shortcut past the tier curve. Routed
through the same evolution check a battle win uses, so a combine that crosses a threshold evolves
the kept mon exactly as a win would. Works by clicking a duplicate in the modal, or by **dragging
one card onto another** directly — the drop only intercepts when the two actually share a line;
otherwise it falls through to the normal move-between-groups behaviour.

**Only the mons that fought earn it.** Anyone who was ever Lead or Support counts; a mon that sat
at the back of the train the whole battle does not, and nor does it get pulled up by the catch-up
floor. Sitting a fight out has to cost something or the line-up order is not a decision. The old
automatic draw (a hash of the mon's id, weighted by its real Health share) is gone;
`healthGrowthPercent` survives on `Species` as a record of that weighting and would be the natural
default if an auto-allocate button ever appears.

### Speed and the charge scale

**100 Speed means three ability activations per attack.** Charge accrues at 3 per point of Speed
per Step against a threshold of 100, and a mon fires once per whole threshold banked — carrying
the remainder, so Speed above the threshold is not wasted. Every species starts at a flat 10
Speed, firing roughly once every three and a half attacks, and the cap is 100.

The old scale was Speed 1-3 against a threshold of 3, which made one point worth doubling or
tripling a mon's entire output. Spreading the same relationship over a hundred points is what
makes Speed something to invest in gradually. The flat starting value is deliberate: the
tier-derived 1-3 spread meant nothing against 100, and re-deriving a per-species spread is a
balance job in its own right.

**The shared golden fixtures run on `LEGACY_CHARGE_CONFIG`.** They were calibrated against Unity's
numbers, and the threshold decides when passives fire and therefore who wins — so on the new scale
all six would fail. Rewriting their expectations would have thrown away the only
cross-implementation guarantee this project has, so charge is parameterised instead and the
fixtures keep testing the original contract.

## Held items

A Pokémon holds at most one. Items are bought at Shops (three on offer, rerolled with the
Pokémon), found in encounters, and moved around by dragging — from the **Bag** popup onto a mon,
from one mon to another, or clicked off to go back in the bag. Giving a mon a second item returns
the first to the bag rather than destroying it.

Each maps to a real Pokémon item where the mechanic has an honest counterpart:

| Item | What it does |
|---|---|
| Lum Berry | Cures a status the moment it lands. Once per battle. |
| Leftovers | Heals a little at the end of every Step. |
| Sitrus Berry | Heals once, the first time the holder drops below half. |
| Lucky Egg | Extra EXP toward evolving from every win. |
| HP Up / Protein / Calcium / Carbos | Flat boost to one stat while held. |
| Power Weight / Bracer / Lens / Anklet | +1 to one stat permanently after every battle fought. |
| Plates (17) + Silk Scarf | **Replaces** the holder's typing with that type. |

The Power items are the closest thing to a literal match — in canon they grant EVs per battle,
which is exactly a small permanent boost after every fight. The Vitamins are a liberty: canon
makes them one-use consumables, but the vitamin-to-stat mapping is the most recognisable one the
games have. Plates are exactly what they do for Arceus, with Silk Scarf covering Normal since
Arceus holds nothing to stay Normal.

Typing items matter more here than in the real games, because typing drives the **team synergies**
rather than a damage chart — a Plate is a way to buy into a synergy you are one mon short of.

The one-shot items (Lum, Sitrus) track their spent state on the *combatant*, not the run, so
"refreshes at the end of battle" needs no reset step anywhere: combatants are rebuilt every fight.

## Node types

| Icon | Node | What it is |
|---|---|---|
| battle | **Wild** | Wild Pokémon. Catchable. Pays EXP and a little money. |
| mystery-trainer | **Mystery Trainer** | Another player's team — the hook for asynchronous multiplayer, generated for now. **Not catchable**, so the reward is money instead, and more of it. |
| encounter | **Encounter** | A branching road event. Usually positive. |
| shop | **Shop** | The Pokémon Center. Sells balls, and offers five region-relevant Pokémon to adopt with a $2 reroll. Pays nothing. Never two layers in a row. |
| gym | **Gym** | Ends the Location and awards a badge. |

A Location runs eight columns: an entry choice of three, six layers of forks, then the Gym. Most
layers offer three options.

## Beating a Gym

A celebration screen gives the badge, the leader's line, and a choice of three regions to travel
to next — each showing the types you are likely to meet there. On arrival you pick one of three
**permanent trainer buffs** flavoured to that region, which last the rest of the run. The buff
*effects* are placeholders; the shape around them is not.

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
- **`SUDDEN_DEATH_STEP` is now wrong and actively interfering.** It is 30, chosen when the longest
  real fight was 17. With tripled Health the longest is 43, so sudden death has gone from firing
  once in four thousand fights to roughly one in sixteen — it decides fights that were resolving
  on their own. Raising it is the first thing the balance pass should do. A test records the
  current figure and will fail when it changes.
- **Special is dormant for most of the roster.** Only 3 of the 20 authored abilities deal damage
  to an enemy, so 17 override the default and never use the stat. That is correct behaviour and a
  content gap, not a bug: the 183 unique abilities the design calls for don't exist yet, and the
  20 shared type-flavoured ones stand in.
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
