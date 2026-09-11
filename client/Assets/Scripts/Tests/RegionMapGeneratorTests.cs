using System.Linq;
using NUnit.Framework;
using Pets.Meta;

namespace Pets.Tests
{
    /// <summary>Coverage for RegionMapGenerator's graph shape (PLAN.md Phase 1, item 5) — this
    /// map is a visual-only prototype, but the graph itself still needs to be well-formed
    /// (single Gym, no unreachable/dead-end nodes) for the map to render sensibly.</summary>
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
        public void Generate_HasExactlyOneGymNode_InTheFinalLayer()
        {
            var map = RegionMapGenerator.Generate(seed: 2, layerCount: 7);

            var gymNodes = map.Nodes.Where(n => n.Type == NodeType.Gym).ToList();
            Assert.AreEqual(1, gymNodes.Count);
            Assert.AreEqual(map.LayerCount - 1, gymNodes[0].Layer);
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
        public void Generate_Throws_WhenLayerCountIsTooSmallToFitAStartAndAGym()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RegionMapGenerator.Generate(seed: 1, layerCount: 2));
        }
    }
}
