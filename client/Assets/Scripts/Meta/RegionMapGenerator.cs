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
        /// layer is always exactly one node/type by design (see ConnectLayers).</summary>
        private const int MinDistinctReachableTypes = 2;

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

                var currentLayer = new List<RegionMapNode>();
                var typeCountsInLayer = new Dictionary<NodeType, int>();
                for (int i = 0; i < nodeCount; i++)
                {
                    var type = PickNodeType(rng, typeCountsInLayer);
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
        /// (MaxOfAnyTypePerLayer generally, the tighter MaxCampPerLayer for Camp). With a layer no
        /// bigger than MaxNodesPerLayer and caps well above what a layer needs to fill, a valid type
        /// is always available — the exception is genuinely unreachable and fails loudly rather than
        /// silently producing a cap-violating layer.</summary>
        private static NodeType PickNodeType(DeterministicRandom rng, Dictionary<NodeType, int> typeCountsInLayer)
        {
            const int maxAttempts = 64;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = MiddleNodeTypes[rng.NextInt(MiddleNodeTypes.Length)];
                int cap = candidate == NodeType.Camp ? MaxCampPerLayer : MaxOfAnyTypePerLayer;
                int current = typeCountsInLayer.TryGetValue(candidate, out var count) ? count : 0;
                if (current < cap)
                {
                    typeCountsInLayer[candidate] = current + 1;
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                $"Could not draw a node type honoring per-layer caps (max {MaxOfAnyTypePerLayer}/type, {MaxCampPerLayer} Camp) after {maxAttempts} attempts.");
        }

        /// <summary>Spreads <paramref name="to"/> across <paramref name="from"/> as a monotonically
        /// advancing staircase of contiguous target ranges: source i owns [lo, hi], and source i+1
        /// starts at that same hi, so ranges touch and every target keeps at least one incoming edge
        /// (nothing unreachable) with every source keeping at least one outgoing edge (no dead
        /// ends).
        ///
        /// Each source's range is then widened — growing lo/hi outward, never leaving gaps — until
        /// it reaches at least MinDistinctReachableTypes distinct node types, so every choice is a
        /// real choice rather than several copies of the same node. That widening can make
        /// neighbouring sources' ranges overlap more than the base staircase would on its own;
        /// reachable-type diversity wins that trade-off over a perfectly uncrossed look. The one
        /// case this can't apply is a single-node target layer (the Gym) — there's only one type to
        /// reach, so every source simply connects to it, same as before.</summary>
        private static void ConnectLayers(List<RegionMapNode> from, List<RegionMapNode> to, DeterministicRandom rng)
        {
            int lo = 0;
            for (int i = 0; i < from.Count; i++)
            {
                int hi;
                if (i == from.Count - 1)
                {
                    // The last source mops up everything still unclaimed so no target is stranded.
                    hi = to.Count - 1;
                }
                else
                {
                    // Advance roughly proportionally, with a one-node jitter so fan-out varies
                    // between layers instead of every node getting an identical share.
                    int ideal = ((i + 1) * (to.Count - 1)) / from.Count;
                    hi = Math.Min(Math.Max(ideal + rng.NextInt(2), lo), to.Count - 1);
                }

                (lo, hi) = WidenForTypeDiversity(to, lo, hi);

                for (int target = lo; target <= hi; target++)
                {
                    from[i].NextIds.Add(to[target].Id);
                }
                lo = hi;
            }
        }

        /// <summary>Grows [lo, hi] outward (alternating sides, starting right) until the range
        /// covers at least MinDistinctReachableTypes distinct types or the whole list — never
        /// shrinking, so every guarantee the caller already established still holds.</summary>
        private static (int lo, int hi) WidenForTypeDiversity(List<RegionMapNode> to, int lo, int hi)
        {
            var reachableTypes = new HashSet<NodeType>();
            for (int i = lo; i <= hi; i++)
            {
                reachableTypes.Add(to[i].Type);
            }

            bool growRightNext = true;
            while (reachableTypes.Count < MinDistinctReachableTypes && (lo > 0 || hi < to.Count - 1))
            {
                if (growRightNext && hi < to.Count - 1)
                {
                    hi++;
                    reachableTypes.Add(to[hi].Type);
                }
                else if (lo > 0)
                {
                    lo--;
                    reachableTypes.Add(to[lo].Type);
                }
                else if (hi < to.Count - 1)
                {
                    hi++;
                    reachableTypes.Add(to[hi].Type);
                }
                growRightNext = !growRightNext;
            }

            return (lo, hi);
        }
    }
}
