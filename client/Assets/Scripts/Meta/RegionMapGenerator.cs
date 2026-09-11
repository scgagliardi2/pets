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

        private const int MinNodesPerLayer = 2;
        private const int MaxNodesPerLayer = 4;

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
                for (int i = 0; i < nodeCount; i++)
                {
                    var type = MiddleNodeTypes[rng.NextInt(MiddleNodeTypes.Length)];
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

        /// <summary>Spreads <paramref name="to"/> across <paramref name="from"/> as a monotonically
        /// advancing staircase of contiguous target ranges: source i owns [lo, hi], and source i+1
        /// starts at that same hi, so ranges touch but never overlap out of order.
        ///
        /// That one rule buys all three properties the map needs at once: every source keeps at
        /// least one outgoing edge (no dead ends), every target keeps at least one incoming edge
        /// (nothing unreachable), and no two edges cross — which is what makes the rendered map
        /// readable as a set of distinct walkable paths instead of a tangle.</summary>
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

                for (int target = lo; target <= hi; target++)
                {
                    from[i].NextIds.Add(to[target].Id);
                }
                lo = hi;
            }
        }
    }
}
