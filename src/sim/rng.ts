/**
 * Seeded PRNG for the battle simulation.
 *
 * ## Why this isn't a straight port
 *
 * Unity's `DeterministicRandom` is xorshift32 seeded directly. That generator is fine in
 * aggregate, but its **first draw is nearly linear in the seed**: seeds 1, 2 and 3 produce 369,
 * 738 and 1107. Consecutive small seeds are therefore not independent, which burned a Unity-side
 * test that rolled 10% odds with `seed = 1, 2, 3...` and saw the first two both succeed
 * (REACT_REBUILD_REFERENCE.md §6.3).
 *
 * Run seeds are exactly the kind of thing that ends up small and consecutive, so this
 * implementation **mixes the seed through splitmix32 before use**. The generator itself is still
 * xorshift32, so the stream algorithm remains the documented, portable contract that
 * `battle-sim-spec.md` §10 asks for — only the seeding changes.
 *
 * ## Parity note
 *
 * Nothing in the simulator currently draws from the RNG: no probabilistic passive exists yet, and
 * every shared golden fixture is fully deterministic without it. So today this divergence is
 * unobservable and costs nothing.
 *
 * The moment a probabilistic effect *is* added, the two implementations will disagree unless
 * Unity adopts the same mixing. Until then, `createLegacyUnityRandom` reproduces Unity's exact
 * stream for cross-checking.
 */

/** A source of deterministic randomness. */
export interface Rng {
  /** A value in [0, exclusiveMax). */
  nextInt(exclusiveMax: number): number;
  /** A value in [0, 1). */
  nextFloat(): number;
  /** True with the given probability in [0, 1]. */
  chance(probability: number): boolean;
}

/** splitmix32 finalizer — cheap, strong avalanche. Used to condition the seed. */
function mixSeed(seed: number): number {
  let z = seed >>> 0;
  z = (z + 0x9e3779b9) >>> 0;
  z = Math.imul(z ^ (z >>> 16), 0x21f0aaad) >>> 0;
  z = Math.imul(z ^ (z >>> 15), 0x735a2d97) >>> 0;
  return (z ^ (z >>> 15)) >>> 0;
}

function makeXorshift32(initialState: number): Rng {
  // xorshift32 has one dead state; 0 must never enter the loop. Unity substitutes the golden
  // ratio constant here and so do we.
  let state = initialState >>> 0 || 0x9e3779b9;

  const nextUInt = (): number => {
    state ^= (state << 13) >>> 0;
    state >>>= 0;
    state ^= state >>> 17;
    state ^= (state << 5) >>> 0;
    state >>>= 0;
    return state;
  };

  return {
    nextInt(exclusiveMax: number): number {
      if (!Number.isInteger(exclusiveMax) || exclusiveMax <= 0) {
        throw new RangeError(`nextInt requires a positive integer bound, got ${exclusiveMax}`);
      }
      return nextUInt() % exclusiveMax;
    },
    nextFloat(): number {
      return nextUInt() / 0x100000000;
    },
    chance(probability: number): boolean {
      if (probability <= 0) return false;
      if (probability >= 1) return true;
      return this.nextFloat() < probability;
    },
  };
}

/**
 * A seed derived from a string — a node id, an instance id, anything that names one thing.
 *
 * FNV-1a, which is small, has no dependencies and avalanches well enough that ids differing only
 * in their last character ("L0-1-0" and "L0-1-1") land far apart. That last property is the whole
 * point: seeds built from a node's *position* alone gave every node in a layer the same encounter,
 * because the position is what they have in common.
 *
 * The result is a signed 32-bit integer, which `createRandom` handles — it takes the seed through
 * `>>> 0` before use.
 */
export function hashString(text: string): number {
  let hash = 2166136261;
  for (let i = 0; i < text.length; i++) {
    hash = Math.imul(hash ^ text.charCodeAt(i), 16777619);
  }
  return hash | 0;
}

/**
 * The standard battle RNG: seed conditioned through splitmix32, stream from xorshift32.
 * One instance per battle (battle-sim-spec.md §10).
 */
export function createRandom(seed: number): Rng {
  return makeXorshift32(mixSeed(seed));
}

/**
 * Unity's exact stream — xorshift32 seeded with no conditioning. Kept only so a test can
 * demonstrate the seed-correlation problem, and so a future cross-implementation check has
 * something to compare against. Don't use this to run a fight.
 */
export function createLegacyUnityRandom(seed: number): Rng {
  return makeXorshift32(seed >>> 0);
}
