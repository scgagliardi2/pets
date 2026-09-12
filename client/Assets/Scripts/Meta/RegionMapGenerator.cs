using System;
using System.Collections.Generic;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Generates a random, branching Slay-the-Spire-style node-map (design doc §5) that
    /// opens on a fixed number of choices and always funnels into a single mandatory Gym node in
    /// its last layer (design doc §4: "always funnels into a single mandatory Gym battle").
    /// The graph produced here is walkable (RegionMapTraversal) and rendered by
    /// RegionMapController, but what a node actually *does* on arrival isn't modeled yet — node
    /// resolution (fights, events, camps) is the next Phase 1 step (PLAN.md).</summary>
    public static class RegionMapGenerator
    {
        /// <summary>The start node always opens onto exactly this many choices, so a run's first
        /// decision is a consistent shape rather than a roll of the dice.</summary>
        public const int StartingOptionCount = 3;

        /// <summary>Layers of player choice between the start node and the Gym.</summary>
        public const int ChoiceLayerCount = 5;

        /// <summary>One start layer + the choice layers + one Gym layer.</summary>
        public const int DefaultLayerCount = ChoiceLayerCount + 2;

        private static readonly NodeType[] MiddleNodeTypes =
        {
            NodeType.PvE, NodeType.PvE, NodeType.PvE, NodeType.Event, NodeType.PvP, NodeType.Camp
        };

        private const int MinNodesPerLayer = 3;
        private const int MaxNodesPerLayer = 4;

        /// <summary>No choice layer offers more than this many nodes of the same type — keeps any
        /// one layer from reading as "mostly one thing".</summary>
        private const int MaxOfAnyTypePerLayer = 2;

        /// <summary>Camp (shown to the player as "Pokémon Center") is capped tighter than the
        /// general per-type limit — at most one per layer, so healing/shopping stays a deliberate,
        /// occasional stop rather than a repeat option.</summary>
        private const int MaxCampPerLayer = 1;

        /// <summary>From any node, the set of nodes it can step to must include at least this many
        /// distinct types — so a choice is always actually a choice, never just "which copy of the
        /// same node." Doesn't apply to the final choice layer's edges into the Gym, since the Gym
        /// layer is always exactly one node/type by design (see ConnectLayers); nor to the rare
        /// single-connection case ApplyRareSingleConnection can deliberately introduce; nor,
        /// best-effort, to the leftmost/rightmost source in a layer transition where the
        /// opposite-edge rule's bound makes 2 types structurally unreachable — the edge rule is the
        /// harder constraint of the two when they conflict (see edgeRuleApplies in ConnectLayers).</summary>
        private const int MinDistinctReachableTypes = 2;

        /// <summary>Pokémon Center (Camp) is a deliberate checkpoint, not a random drop-in: it can
        /// only land on this fixed mid-run choice layer or on the layer directly before the Gym
        /// (see the campAllowed check in Generate), so healing/shopping shows up as a predictable
        /// beat rather than wherever the type roll happens to land it.</summary>
        private const int PokeCenterMidRunLayer = 3;

        /// <summary>Chance (of 100) that a fan-out of exactly 3 nodes is left alone instead of
        /// nudged to 2 or 4 — connecting to 3 nodes in the next layer should read as an uncommon
        /// shape, not a default one.</summary>
        private const int TripleFanOutKeepChancePercent = 15;

        /// <summary>Chance (of 100) that an otherwise multi-node fan-out is collapsed down to a
        /// single connection, when doing so is safe (see ApplyRareSingleConnection) — a rare,
        /// deliberately narrow path rather than the norm.</summary>
        private const int SingleConnectionChancePercent = 8;

        /// <summary>layerCount includes the single start layer and the single Gym layer, so the
        /// minimum meaningful map is 3 layers (start -> one choice layer -> Gym).</summary>
        public static RegionMap Generate(int seed, int layerCount = DefaultLayerCount)
        {
            if (layerCount < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(layerCount), "Need at least a start layer, one middle layer, and a Gym layer.");
            }

            var rng = new DeterministicRandom(seed);
            var map = new RegionMap { LayerCount = layerCount, Seed = seed };

            // The start node is a "you are here" marker at the Location entrance rather than a
            // choice — the player's first decision is which of layer 1's nodes to step onto.
            var start = new RegionMapNode { Id = "L0-0", Type = NodeType.PvE, Layer = 0, IndexInLayer = 0 };
            map.Nodes.Add(start);
            map.StartNodeId = start.Id;

            var previousLayer = new List<RegionMapNode> { start };
            for (int layer = 1; layer < layerCount - 1; layer++)
            {
                int nodeCount = layer == 1
                    ? StartingOptionCount
                    : MinNodesPerLayer + rng.NextInt(MaxNodesPerLayer - MinNodesPerLayer + 1);

                bool campAllowed = layer == PokeCenterMidRunLayer || layer == layerCount - 2;

                var currentLayer = new List<RegionMapNode>();
                var typeCountsInLayer = new Dictionary<NodeType, int>();
                for (int i = 0; i < nodeCount; i++)
                {
                    var type = PickNodeType(rng, typeCountsInLayer, campAllowed);
                    currentLayer.Add(new RegionMapNode { Id = $"L{layer}-{i}", Type = type, Layer = layer, IndexInLayer = i });
                }
                map.Nodes.AddRange(currentLayer);
                ConnectLayers(previousLayer, currentLayer, rng);
                previousLayer = currentLayer;
            }

            var gym = new RegionMapNode { Id = $"L{layerCount - 1}-0", Type = NodeType.Gym, Layer = layerCount - 1, IndexInLayer = 0 };
            map.Nodes.Add(gym);
            map.GymNodeId = gym.Id;
            ConnectLayers(previousLayer, new List<RegionMapNode> { gym }, rng);

            return map;
        }

        /// <summary>Draws a node type for a layer from the same weighted pool as always (PvE most
        /// common), re-rolling any draw that would push a type over its per-layer cap
        /// (MaxOfAnyTypePerLayer generally, the tighter MaxCampPerLayer for Camp) or that draws Camp
        /// in a layer where it isn't allowed to appear at all (<paramref name="campAllowed"/>). With
        /// a layer no bigger than MaxNodesPerLayer and caps well above what a layer needs to fill, a
        /// valid type is always available — the exception is genuinely unreachable and fails loudly
        /// rather than silently producing a cap-violating layer.</summary>
        private static NodeType PickNodeType(DeterministicRandom rng, Dictionary<NodeType, int> typeCountsInLayer, bool campAllowed)
        {
            const int maxAttempts = 64;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = MiddleNodeTypes[rng.NextInt(MiddleNodeTypes.Length)];
                if (candidate == NodeType.Camp && !campAllowed)
                {
                    continue;
                }

                int cap = candidate == NodeType.Camp ? MaxCampPerLayer : MaxOfAnyTypePerLayer;
                int current = typeCountsInLayer.TryGetValue(candidate, out var count) ? count : 0;
                if (current < cap)
                {
                    typeCountsInLayer[candidate] = current + 1;
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                $"Could not draw a node type honoring per-layer caps (max {MaxOfAnyTypePerLayer}/type, {MaxCampPerLayer} Camp, campAllowed={campAllowed}) after {maxAttempts} attempts.");
        }

        /// <summary>Spreads <paramref name="to"/> across <paramref name="from"/> as a monotonically
        /// advancing staircase of contiguous target ranges: source i owns [lo, hi], and source i+1
        /// starts at that same hi, so ranges touch and every target keeps at least one incoming edge
        /// (nothing unreachable) with every source keeping at least one outgoing edge (no dead
        /// ends).
        ///
        /// Three shaping passes run on top of that base staircase, in order:
        /// 1. AvoidRareTripleFanOut nudges a fresh 3-node-wide range to 2 or 4 most of the time — a
        ///    node connecting to exactly 3 others is meant to read as an uncommon shape.
        /// 2. WidenForTypeDiversity grows lo/hi outward, never shrinking, until the range reaches at
        ///    least MinDistinctReachableTypes distinct node types, so every choice is a real choice.
        ///    This can re-widen a range step 1 just narrowed, and can make neighbouring ranges
        ///    overlap more than the base staircase would on its own — reachable-type diversity wins
        ///    that trade-off over a perfectly uncrossed look or a rare-shape guarantee.
        /// 3. ApplyRareSingleConnection, once every source has a range, occasionally collapses a
        ///    multi-node range down to one node — but only where every other node it would stop
        ///    covering still has another incoming source, so nothing is stranded.
        ///
        /// Two structural caps bound where ranges can sit at all: a single-node target layer (the
        /// Gym) skips all of the above, since there's only one node/type to reach; and where both
        /// layers have more than one node, the leftmost source can't reach the rightmost target and
        /// the rightmost source can't reach the leftmost target (see edgeRuleApplies) — no edge
        /// connects straight across to the opposite edge of the next row. The leftmost target is
        /// always covered by the leftmost source (whose range always starts at lo=0) and the
        /// rightmost target is always covered by the rightmost source (which always mops up through
        /// hi=to.Count-1), so forbidding the opposite-edge reach never strands either end.</summary>
        private static void ConnectLayers(List<RegionMapNode> from, List<RegionMapNode> to, DeterministicRandom rng)
        {
            bool edgeRuleApplies = from.Count > 1 && to.Count > 1;
            var ranges = new (int lo, int hi)[from.Count];

            int lo = 0;
            for (int i = 0; i < from.Count; i++)
            {
                bool isLast = i == from.Count - 1;
                int maxHi = edgeRuleApplies && i == 0 ? to.Count - 2 : to.Count - 1;
                int minLo = edgeRuleApplies && isLast ? 1 : 0;

                int hi;
                if (isLast)
                {
                    // The last source mops up everything still unclaimed so no target is stranded.
                    hi = to.Count - 1;
                    lo = Math.Max(lo, minLo);
                }
                else
                {
                    // Advance roughly proportionally, with a one-node jitter so fan-out varies
                    // between layers instead of every node getting an identical share.
                    int ideal = ((i + 1) * (to.Count - 1)) / from.Count;
                    hi = Math.Min(Math.Max(ideal + rng.NextInt(2), lo), maxHi);
                }

                // A lone source (e.g. the start node into layer 1) must keep its full range —
                // it's the only path to every target in "to", so it has nothing to spare.
                if (from.Count > 1)
                {
                    (lo, hi) = AvoidRareTripleFanOut(rng, lo, hi, minLo, maxHi, hiIsFixed: isLast);
                }
                (lo, hi) = WidenForTypeDiversity(to, lo, hi, minLo, maxHi);

                ranges[i] = (lo, hi);
                lo = hi;
            }

            ApplyRareSingleConnection(rng, to, ranges);

            for (int i = 0; i < from.Count; i++)
            {
                for (int target = ranges[i].lo; target <= ranges[i].hi; target++)
                {
                    from[i].NextIds.Add(to[target].Id);
                }
            }
        }

        /// <summary>Grows [lo, hi] outward (alternating sides, starting right) until the range
        /// covers at least MinDistinctReachableTypes distinct types or fills [minLo, maxHi] — never
        /// shrinking, so every guarantee the caller already established still holds, and never
        /// crossing minLo/maxHi, so the opposite-edge rule survives widening.</summary>
        private static (int lo, int hi) WidenForTypeDiversity(List<RegionMapNode> to, int lo, int hi, int minLo, int maxHi)
        {
            var reachableTypes = new HashSet<NodeType>();
            for (int i = lo; i <= hi; i++)
            {
                reachableTypes.Add(to[i].Type);
            }

            bool growRightNext = true;
            while (reachableTypes.Count < MinDistinctReachableTypes && (lo > minLo || hi < maxHi))
            {
                if (growRightNext && hi < maxHi)
                {
                    hi++;
                    reachableTypes.Add(to[hi].Type);
                }
                else if (lo > minLo)
                {
                    lo--;
                    reachableTypes.Add(to[lo].Type);
                }
                else if (hi < maxHi)
                {
                    hi++;
                    reachableTypes.Add(to[hi].Type);
                }
                growRightNext = !growRightNext;
            }

            return (lo, hi);
        }

        /// <summary>If [lo, hi] is exactly 3 nodes wide, nudges it to 2 or 4 most of the time
        /// (TripleFanOutKeepChancePercent of the time it's left alone) by moving whichever end isn't
        /// pinned down: <paramref name="hiIsFixed"/> sources (the staircase's mop-up source, which
        /// must keep hi at the target layer's last index) get nudged via lo instead. Only moves
        /// within [minLo, maxHi], so this never reopens the opposite-edge rule those bounds enforce;
        /// if neither direction is free, the range is left at width 3.</summary>
        private static (int lo, int hi) AvoidRareTripleFanOut(DeterministicRandom rng, int lo, int hi, int minLo, int maxHi, bool hiIsFixed)
        {
            if (hi - lo + 1 != 3 || rng.NextInt(100) < TripleFanOutKeepChancePercent)
            {
                return (lo, hi);
            }

            if (hiIsFixed)
            {
                bool canNarrow = lo + 1 <= hi;
                bool canWiden = lo - 1 >= minLo;
                if (canNarrow && canWiden)
                {
                    lo += rng.NextInt(2) == 0 ? 1 : -1;
                }
                else if (canNarrow)
                {
                    lo += 1;
                }
                else if (canWiden)
                {
                    lo -= 1;
                }
            }
            else
            {
                bool canNarrow = hi - 1 >= lo;
                bool canWiden = hi + 1 <= maxHi;
                if (canNarrow && canWiden)
                {
                    hi += rng.NextInt(2) == 0 ? -1 : 1;
                }
                else if (canNarrow)
                {
                    hi -= 1;
                }
                else if (canWiden)
                {
                    hi += 1;
                }
            }

            return (lo, hi);
        }

        /// <summary>With small probability (SingleConnectionChancePercent), collapses a source's
        /// multi-node range down to a single node — a rare, deliberately narrow path. Only does so
        /// when it's safe: every node the range would stop covering must still have at least one
        /// other source reaching it, so this can never strand a target. Runs once, after every
        /// source's range is finalized, using a shared coverage count so an earlier collapse in this
        /// same layer transition is accounted for before a later one is attempted. Exempt when
        /// there's only one source in this layer transition, since it alone must cover every target
        /// and has nothing to spare.</summary>
        private static void ApplyRareSingleConnection(DeterministicRandom rng, List<RegionMapNode> to, (int lo, int hi)[] ranges)
        {
            if (ranges.Length <= 1)
            {
                return;
            }

            var coverage = new int[to.Count];
            foreach (var (rangeLo, rangeHi) in ranges)
            {
                for (int target = rangeLo; target <= rangeHi; target++)
                {
                    coverage[target]++;
                }
            }

            for (int i = 0; i < ranges.Length; i++)
            {
                var (lo, hi) = ranges[i];
                if (lo == hi || rng.NextInt(100) >= SingleConnectionChancePercent)
                {
                    continue;
                }

                for (int keep = lo; keep <= hi; keep++)
                {
                    bool safeToCollapse = true;
                    for (int target = lo; target <= hi; target++)
                    {
                        if (target != keep && coverage[target] <= 1)
                        {
                            safeToCollapse = false;
                            break;
                        }
                    }

                    if (!safeToCollapse)
                    {
                        continue;
                    }

                    for (int target = lo; target <= hi; target++)
                    {
                        if (target != keep)
                        {
                            coverage[target]--;
                        }
                    }

                    ranges[i] = (keep, keep);
                    break;
                }
            }
        }
    }
}
