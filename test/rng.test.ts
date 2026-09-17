/**
 * PRNG behaviour, and specifically the seed-correlation trap from
 * REACT_REBUILD_REFERENCE.md §6.3 that burned a test in the Unity build.
 */

import { describe, expect, it } from 'vitest';

import { createLegacyUnityRandom, createRandom } from '../src/sim/index.js';

describe('the problem this PRNG setup exists to avoid', () => {
  it('demonstrates that raw xorshift32 first draws are linear in the seed', () => {
    // Unity's generator, unconditioned. The reference doc records seeds 1, 2, 3 producing
    // 369, 738 and 1107 — those are nextInt(10000) values.
    const first = (seed: number) => createLegacyUnityRandom(seed).nextInt(10_000);

    expect(first(1)).toBe(369);
    expect(first(2)).toBe(738);
    expect(first(3)).toBe(1107);

    // Underneath, the relationship isn't merely "nearly" linear — it is exact. The first draw
    // for seed N is N times the first draw for seed 1.
    const raw = (seed: number) => createLegacyUnityRandom(seed).nextInt(2 ** 32 - 1);
    expect(raw(2)).toBe(raw(1) * 2);
    expect(raw(3)).toBe(raw(1) * 3);
  });

  it('shows the practical consequence: consecutive small seeds are not independent', () => {
    // Rolling 10% odds across seeds 1..3 with the raw generator: the linearity means the low
    // seeds cluster instead of behaving like independent draws.
    const raw = [1, 2, 3].map((s) => createLegacyUnityRandom(s).nextFloat());
    expect(raw[1]! / raw[0]!).toBeCloseTo(2, 5);
    expect(raw[2]! / raw[0]!).toBeCloseTo(3, 5);
  });

  it('the conditioned generator breaks that correlation', () => {
    const mixed = [1, 2, 3].map((s) => createRandom(s).nextFloat());

    // No fixed ratio between consecutive seeds any more.
    expect(mixed[1]! / mixed[0]!).not.toBeCloseTo(2, 2);
    expect(mixed[2]! / mixed[0]!).not.toBeCloseTo(3, 2);
  });

  it('spreads the first draw of consecutive small seeds across the range', () => {
    const firstDraws = Array.from({ length: 64 }, (_, i) => createRandom(i + 1).nextFloat());
    const lowHalf = firstDraws.filter((v) => v < 0.5).length;

    // A linear generator puts almost all of 64 small seeds in the bottom of the range.
    expect(lowHalf).toBeGreaterThan(16);
    expect(lowHalf).toBeLessThan(48);
  });
});

describe('determinism', () => {
  it('reproduces the same stream for the same seed', () => {
    const a = createRandom(12345);
    const b = createRandom(12345);
    const drawsA = Array.from({ length: 20 }, () => a.nextInt(1000));
    const drawsB = Array.from({ length: 20 }, () => b.nextInt(1000));

    expect(drawsA).toEqual(drawsB);
  });

  it('gives different streams for different seeds', () => {
    const a = Array.from({ length: 10 }, (_, i) => i).map(() => createRandom(1).nextInt(1000));
    const b = Array.from({ length: 10 }, (_, i) => i).map(() => createRandom(2).nextInt(1000));

    expect(a).not.toEqual(b);
  });

  it('survives a seed of 0, which is xorshift32 dead state', () => {
    const rng = createRandom(0);
    const draws = Array.from({ length: 10 }, () => rng.nextInt(1000));

    expect(new Set(draws).size).toBeGreaterThan(1);
  });

  it('handles negative seeds', () => {
    const rng = createRandom(-42);
    expect(() => rng.nextInt(100)).not.toThrow();
  });
});

describe('bounds', () => {
  it('nextInt stays within [0, max)', () => {
    const rng = createRandom(7);
    for (let i = 0; i < 2000; i++) {
      const v = rng.nextInt(6);
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThan(6);
    }
  });

  it('nextFloat stays within [0, 1)', () => {
    const rng = createRandom(7);
    for (let i = 0; i < 2000; i++) {
      const v = rng.nextFloat();
      expect(v).toBeGreaterThanOrEqual(0);
      expect(v).toBeLessThan(1);
    }
  });

  it('rejects a non-positive bound rather than returning nonsense', () => {
    const rng = createRandom(1);
    expect(() => rng.nextInt(0)).toThrow(RangeError);
    expect(() => rng.nextInt(-1)).toThrow(RangeError);
  });

  it('treats certain and impossible chances without drawing', () => {
    const rng = createRandom(1);
    expect(rng.chance(0)).toBe(false);
    expect(rng.chance(1)).toBe(true);
  });

  it('is roughly uniform in aggregate when drawn from one stream', () => {
    // Drawn from ONE stream rather than re-seeding, per §6.3's guidance for tests.
    const rng = createRandom(99);
    const buckets = new Array(10).fill(0) as number[];
    for (let i = 0; i < 100_000; i++) buckets[rng.nextInt(10)]!++;

    for (const count of buckets) {
      expect(count).toBeGreaterThan(9000);
      expect(count).toBeLessThan(11000);
    }
  });
});
