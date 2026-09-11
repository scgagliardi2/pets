using System;
using System.Collections.Generic;
using System.Linq;
using Pets.Simulation;

namespace Pets.Meta
{
    /// <summary>Generates a random, branching Slay-the-Spire-style node-map (design doc §5) that
    /// always funnels into a single mandatory Gym node in its last layer (design doc §4: "always
    /// funnels into a single mandatory Gym battle"). Visual-only prototype for now (PLAN.md Phase 1
    /// item 5) — nothing here is resolvable/playable; only Forest's linear map
    /// (ForestLocationFactory + LocationFlowController) actually runs battles today.</summary>
    public static class RegionMapGenerator
    {
        private static readonly NodeType[] MiddleNodeTypes =
        {
            NodeType.PvE, NodeType.PvE, NodeType.PvE, NodeType.Event, NodeType.PvP, NodeType.Camp
        };

        private const int MinNodesPerLayer = 2;
        private const int MaxNodesPerLayer = 4;

        /// <summary>layerCount includes the single start layer and the single Gym layer, so the
        /// minimum meaningful map is 3 layers (start -> one branching layer -> Gym).</summary>
        public static RegionMap Generate(int seed, int layerCount = 7)
        {
            if (layerCount < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(layerCount), "Need at least a start layer, one middle layer, and a Gym layer.");
            }

            var rng = new DeterministicRandom(seed);
            var map = new RegionMap { LayerCount = layerCount };

            var start = new RegionMapNode { Id = "L0-0", Type = NodeType.PvE, Layer = 0, IndexInLayer = 0 };
            map.Nodes.Add(start);
            map.StartNodeId = start.Id;

            var previousLayer = new List<RegionMapNode> { start };
            for (int layer = 1; layer < layerCount - 1; layer++)
            {
                int nodeCount = MinNodesPerLayer + rng.NextInt(MaxNodesPerLayer - MinNodesPerLayer + 1);
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
            foreach (var node in previousLayer)
            {
                node.NextIds.Add(gym.Id);
            }

            return map;
        }

        /// <summary>Connects each node in `from` to 1-2 random nodes in `to`, then guarantees
        /// every node in `to` has at least one incoming edge so nothing is unreachable.</summary>
        private static void ConnectLayers(List<RegionMapNode> from, List<RegionMapNode> to, DeterministicRandom rng)
        {
            foreach (var node in from)
            {
                int edgeCount = Math.Min(1 + rng.NextInt(2), to.Count);
                var targets = new HashSet<int>();
                while (targets.Count < edgeCount)
                {
                    targets.Add(rng.NextInt(to.Count));
                }
                foreach (var targetIndex in targets)
                {
                    node.NextIds.Add(to[targetIndex].Id);
                }
            }

            for (int i = 0; i < to.Count; i++)
            {
                bool hasIncoming = from.Any(n => n.NextIds.Contains(to[i].Id));
                if (!hasIncoming)
                {
                    from[rng.NextInt(from.Count)].NextIds.Add(to[i].Id);
                }
            }
        }
    }
}
