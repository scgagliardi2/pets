using System;

namespace Pets.Simulation
{
    /// <summary>
    /// Small seeded xorshift32 PRNG — deliberately not System.Random, so the algorithm is a fixed,
    /// documented contract a future server-side reimplementation can match exactly. See
    /// battle-sim-spec.md §7.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = unchecked((uint)seed);
            if (state == 0)
            {
                state = 0x9E3779B9;
            }
        }

        private uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        /// <summary>Returns a value in [0, exclusiveMax).</summary>
        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }
            return (int)(NextUInt() % (uint)exclusiveMax);
        }
    }
}
