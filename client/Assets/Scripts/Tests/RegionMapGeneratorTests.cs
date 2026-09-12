using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pets.Meta;

namespace Pets.Tests
{
    /// <summary>Coverage for RegionMapGenerator's graph shape (PLAN.md Phase 1). The map is
    /// walkable (see RegionMapTraversalTests), so a malformed graph isn't just an ugly render — a
    /// dead end or an unreachable node would strand the player short of the Gym.</summary>
    public class RegionMapGeneratorTests
    {
        [Test]
        public void Generate_ProducesExactlyTheRequestedLayerCount()
        {
            var map = RegionMapGenerator.Generate(seed: 1, layerCount: 6);

            Assert.AreEqual(6, map.LayerCount);
            Assert.AreEqual(5, map.Nodes.Max(n => n.Layer));
            Assert.AreEqual(0, map.Nodes.Min(n => n.Layer));
        }

        [Test]
        public void Generate_DefaultsToAStartLayer_FiveChoiceLayers_AndAGymLayer()
        {
            var map = RegionMapGenerator.Generate(seed: 11);

            Assert.AreEqual(RegionMapGenerator.ChoiceLayerCount + 2, map.LayerCount);
            Assert.AreEqual(1, map.NodesInLayer(0).Count);
            Assert.AreEqual(NodeType.Gym, map.GetById(map.GymNodeId).Type);
            for (int layer = 1; layer <= RegionMapGenerator.ChoiceLayerCount; layer++)
            {
                Assert.IsNotEmpty(map.NodesInLayer(layer), $"choice layer {layer} is empty");
            }
        }

        [Test]
        public void Generate_AlwaysOpensTheRunOnExactlyThreeOptions()
        {
            // The opening choice is a fixed shape rather than a roll, so check a spread of seeds.
            for (int seed = 1; seed <= 25; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                var start = map.GetById(map.StartNodeId);

                Assert.AreEqual(RegionMapGenerator.StartingOptionCount, map.NodesInLayer(1).Count, $"seed {seed}");
                Assert.AreEqual(RegionMapGenerator.StartingOptionCount, start.NextIds.Count, $"seed {seed}");
                CollectionAssert.AllItemsAreUnique(start.NextIds, $"seed {seed}");
            }
        }

        [Test]
        public void Generate_HasExactlyOneGymNode_InTheFinalLayer_ThatEveryPrecedingNodeFeedsInto()
        {
            var map = RegionMapGenerator.Generate(seed: 2, layerCount: 7);

            var gymNodes = map.Nodes.Where(n => n.Type == NodeType.Gym).ToList();
            Assert.AreEqual(1, gymNodes.Count);
            Assert.AreEqual(map.LayerCount - 1, gymNodes[0].Layer);
            Assert.AreEqual(gymNodes[0].Id, map.GymNodeId);

            foreach (var node in map.NodesInLayer(map.LayerCount - 2))
            {
                CollectionAssert.Contains(node.NextIds, map.GymNodeId);
            }
        }

        [Test]
        public void Generate_EveryNonFinalNode_HasAtLeastOneOutgoingEdge()
        {
            var map = RegionMapGenerator.Generate(seed: 3, layerCount: 8);

            foreach (var node in map.Nodes.Where(n => n.Layer < map.LayerCount - 1))
            {
                Assert.IsNotEmpty(node.NextIds, $"{node.Id} has no outgoing edges");
            }
        }

        [Test]
        public void Generate_EveryNonStartNode_IsReachableFromTheLayerBefore()
        {
            var map = RegionMapGenerator.Generate(seed: 4, layerCount: 8);

            foreach (var node in map.Nodes.Where(n => n.Id != map.StartNodeId))
            {
                bool hasIncoming = map.Nodes.Any(other => other.NextIds.Contains(node.Id));
                Assert.IsTrue(hasIncoming, $"{node.Id} has no incoming edge and would be unreachable");
            }
        }

        [Test]
        public void Generate_EveryEdge_PointsToTheImmediatelyNextLayer()
        {
            var map = RegionMapGenerator.Generate(seed: 5, layerCount: 6);

            foreach (var node in map.Nodes)
            {
                foreach (var nextId in node.NextIds)
                {
                    var target = map.GetById(nextId);
                    Assert.AreEqual(node.Layer + 1, target.Layer, $"{node.Id} -> {nextId} skips a layer");
                }
            }
        }

        [Test]
        public void Generate_EveryChoiceLayer_HasAtLeastThreeNodes()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                for (int layer = 1; layer <= RegionMapGenerator.ChoiceLayerCount; layer++)
                {
                    Assert.GreaterOrEqual(map.NodesInLayer(layer).Count, 3, $"seed {seed}, layer {layer}");
                }
            }
        }

        [Test]
        public void Generate_EveryChoiceLayer_HasAtMostTwoOfAnyType_AndAtMostOnePokemonCenter()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                for (int layer = 1; layer <= RegionMapGenerator.ChoiceLayerCount; layer++)
                {
                    var countsByType = map.NodesInLayer(layer).GroupBy(n => n.Type).ToDictionary(g => g.Key, g => g.Count());
                    foreach (var kvp in countsByType)
                    {
                        int cap = kvp.Key == NodeType.Camp ? 1 : 2;
                        Assert.LessOrEqual(kvp.Value, cap, $"seed {seed}, layer {layer}, type {kvp.Key}");
                    }
                }
            }
        }

        [Test]
        public void Generate_EveryNode_CanReachAtLeastTwoDistinctNodeTypes_ExceptWhereAKnownRuleOverridesIt()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                foreach (var node in map.Nodes.Where(n => n.NextIds.Count > 0))
                {
                    var fromLayer = map.NodesInLayer(node.Layer);
                    var targetLayer = map.GetById(node.NextIds[0]).Layer;
                    var toLayer = map.NodesInLayer(targetLayer);

                    if (toLayer.Count == 1)
                    {
                        // Only the Gym layer is ever a single node — nothing to diversify into.
                        continue;
                    }

                    if (node.NextIds.Count == 1)
                    {
                        // The rare single-connection case deliberately trades diversity for a
                        // narrow path (see ApplyRareSingleConnection) — nothing to check here.
                        continue;
                    }

                    bool isEdgeSource = fromLayer.Count > 1 &&
                        (node.IndexInLayer == 0 || node.IndexInLayer == fromLayer.Count - 1);
                    if (isEdgeSource)
                    {
                        // The opposite-edge rule can bound a leftmost/rightmost source's range so
                        // tightly that 2 types genuinely aren't reachable — the edge rule wins that
                        // conflict over diversity (see MinDistinctReachableTypes's doc comment).
                        continue;
                    }

                    var reachableTypes = node.NextIds.Select(id => map.GetById(id).Type).Distinct().Count();
                    Assert.GreaterOrEqual(reachableTypes, 2, $"seed {seed}: {node.Id} only reaches one node type");
                }
            }
        }

        [Test]
        public void Generate_GivesEveryNodeAContiguousRangeOfTargets()
        {
            // A node fanning out to a gapped set (targets 0 and 2 but not 1) would render as two
            // paths straddling a third — the staircase connector is supposed to prevent that.
            var map = RegionMapGenerator.Generate(seed: 9, layerCount: 8);

            foreach (var node in map.Nodes.Where(n => n.NextIds.Count > 0))
            {
                var indices = TargetIndices(map, node);
                Assert.AreEqual(indices.Max() - indices.Min() + 1, indices.Count, $"{node.Id} has gapped targets");
                CollectionAssert.AllItemsAreUnique(node.NextIds, $"{node.Id} has duplicate edges");
            }
        }

        [Test]
        public void Generate_IsDeterministic_ForTheSameSeed()
        {
            var a = RegionMapGenerator.Generate(seed: 42, layerCount: 7);
            var b = RegionMapGenerator.Generate(seed: 42, layerCount: 7);

            Assert.AreEqual(a.Nodes.Count, b.Nodes.Count);
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                Assert.AreEqual(a.Nodes[i].Id, b.Nodes[i].Id);
                Assert.AreEqual(a.Nodes[i].Type, b.Nodes[i].Type);
                CollectionAssert.AreEqual(a.Nodes[i].NextIds, b.Nodes[i].NextIds);
            }
        }

        [Test]
        public void Generate_ProducesDifferentMaps_ForDifferentSeeds()
        {
            var shapes = new HashSet<string>();
            for (int seed = 1; seed <= 10; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                shapes.Add(string.Join("|", map.Nodes.Select(n => $"{n.Id}:{n.Type}:{string.Join(",", n.NextIds)}")));
            }

            Assert.Greater(shapes.Count, 1, "every seed produced an identical map");
        }

        [Test]
        public void Generate_Throws_WhenLayerCountIsTooSmallToFitAStartAndAGym()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RegionMapGenerator.Generate(seed: 1, layerCount: 2));
        }

        [Test]
        public void Generate_OnlyPlacesPokemonCenters_InLayerThree_OrTheLayerRightBeforeTheGym()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var map = RegionMapGenerator.Generate(seed);
                int layerBeforeGym = map.LayerCount - 2;

                foreach (var node in map.Nodes.Where(n => n.Type == NodeType.Camp))
                {
                    Assert.IsTrue(node.Layer == 3 || node.Layer == layerBeforeGym,
                        $"seed {seed}: {node.Id} is a Pokémon Center outside the allowed layers (3, {layerBeforeGym})");
                }
            }
        }

        [Test]
        public void Generate_NeverConnectsAnEdgeNode_ToTheOppositeEdgeOfTheNextLayer()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var map = RegionMapGenerator.Generate(seed, layerCount: 8);
                for (int layer = 0; layer < map.LayerCount - 1; layer++)
                {
                    var from = map.NodesInLayer(layer);
                    var to = map.NodesInLayer(layer + 1);
                    if (from.Count <= 1 || to.Count <= 1)
                    {
                        // A lone source must reach everything, and a lone target has no "opposite
                        // edge" to speak of — the rule only applies once both sides branch.
                        continue;
                    }

                    var leftmostSource = from.First(n => n.IndexInLayer == 0);
                    var rightmostSource = from.First(n => n.IndexInLayer == from.Count - 1);
                    var leftmostTargetId = to.First(n => n.IndexInLayer == 0).Id;
                    var rightmostTargetId = to.First(n => n.IndexInLayer == to.Count - 1).Id;

                    CollectionAssert.DoesNotContain(leftmostSource.NextIds, rightmostTargetId,
                        $"seed {seed}, layer {layer}: leftmost node reaches the opposite (rightmost) edge");
                    CollectionAssert.DoesNotContain(rightmostSource.NextIds, leftmostTargetId,
                        $"seed {seed}, layer {layer}: rightmost node reaches the opposite (leftmost) edge");
                }
            }
        }

        [Test]
        public void Generate_MakesThreeNodeFanOuts_RarerThanOtherFanOutWidths()
        {
            // Statistical check across many seeds/layers: a fan-out to exactly 3 nodes should be
            // noticeably less common than fanning out to 2 or 4, not just an equally likely shape.
            var widthCounts = new Dictionary<int, int>();
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = RegionMapGenerator.Generate(seed, layerCount: 8);
                // Restrict to middle-layer-to-middle-layer edges: the start layer always fans out
                // to exactly StartingOptionCount nodes, and the layer before the Gym always fans
                // out to exactly one (the Gym), so both would skew a "how common is width X" count.
                foreach (var node in map.Nodes.Where(n => n.NextIds.Count > 0 && n.Layer >= 1 && n.Layer < map.LayerCount - 2))
                {
                    int width = node.NextIds.Count;
                    widthCounts[width] = widthCounts.TryGetValue(width, out var count) ? count + 1 : 1;
                }
            }

            int width3Count = widthCounts.TryGetValue(3, out var w3) ? w3 : 0;
            int width2Count = widthCounts.TryGetValue(2, out var w2) ? w2 : 0;
            int width4Count = widthCounts.TryGetValue(4, out var w4) ? w4 : 0;

            Assert.Greater(width2Count + width4Count, 0, "expected some 2- or 4-wide fan-outs to compare against");
            Assert.Less(width3Count, width2Count + width4Count,
                $"3-wide fan-outs ({width3Count}) should be rarer than 2- and 4-wide combined ({width2Count + width4Count})");
        }

        [Test]
        public void Generate_OccasionallyProducesASingleNodeFanOut_OutsideTheGymLayer()
        {
            // Small-chance feature: over enough seeds, at least one non-Gym source should collapse
            // to exactly one outgoing edge (ApplyRareSingleConnection), but only rarely.
            int singleConnectionCount = 0;
            int totalNonGymEdgesFrom = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = RegionMapGenerator.Generate(seed, layerCount: 8);
                // Restrict to middle-layer-to-middle-layer edges: the start layer always fans out
                // to exactly StartingOptionCount nodes, and the layer before the Gym always fans
                // out to exactly one (the Gym), so both would skew a "how common is width X" count.
                foreach (var node in map.Nodes.Where(n => n.NextIds.Count > 0 && n.Layer >= 1 && n.Layer < map.LayerCount - 2))
                {
                    totalNonGymEdgesFrom++;
                    if (node.NextIds.Count == 1)
                    {
                        singleConnectionCount++;
                    }
                }
            }

            Assert.Greater(singleConnectionCount, 0, "expected the rare single-connection case to show up at least once across 200 seeds");
            Assert.Less(singleConnectionCount * 4, totalNonGymEdgesFrom, "single-node fan-outs should stay rare, not become a common shape");
        }

        private static List<int> TargetIndices(RegionMap map, RegionMapNode node) =>
            node.NextIds.Select(id => map.GetById(id).IndexInLayer).ToList();
    }
}
