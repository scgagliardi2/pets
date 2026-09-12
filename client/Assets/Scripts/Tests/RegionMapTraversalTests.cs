using System.Linq;
using NUnit.Framework;
using Pets.Meta;

namespace Pets.Tests
{
    /// <summary>Coverage for walking a generated map (PLAN.md Phase 1) — that movement is confined
    /// to real forward edges, that the walked path is recorded, and that every walk terminates at
    /// the Gym no matter which branches are taken.</summary>
    public class RegionMapTraversalTests
    {
        [Test]
        public void NewTraversal_StartsOnTheStartNode_WithThreeOptions()
        {
            var map = RegionMapGenerator.Generate(seed: 7);
            var traversal = new RegionMapTraversal(map);

            Assert.AreEqual(map.StartNodeId, traversal.CurrentNodeId);
            Assert.AreEqual(RegionMapGenerator.StartingOptionCount, traversal.AvailableNextNodes.Count);
            CollectionAssert.AreEqual(new[] { map.StartNodeId }, traversal.VisitedNodeIds);
            Assert.IsFalse(traversal.IsComplete);
        }

        [Test]
        public void MoveTo_AdvancesToTheChosenNode_AndRecordsIt()
        {
            var map = RegionMapGenerator.Generate(seed: 8);
            var traversal = new RegionMapTraversal(map);
            var chosen = traversal.AvailableNextNodes[1];

            traversal.MoveTo(chosen.Id);

            Assert.AreEqual(chosen.Id, traversal.CurrentNodeId);
            Assert.IsTrue(traversal.HasVisited(chosen.Id));
            CollectionAssert.AreEqual(new[] { map.StartNodeId, chosen.Id }, traversal.VisitedNodeIds);
        }

        [Test]
        public void MoveTo_RejectsANodeTheCurrentNodeIsNotConnectedTo()
        {
            var map = RegionMapGenerator.Generate(seed: 8);
            var traversal = new RegionMapTraversal(map);

            // Layer 2 is a whole layer ahead — reachable eventually, but never in one step.
            var twoLayersAhead = map.NodesInLayer(2).First();

            Assert.IsFalse(traversal.CanMoveTo(twoLayersAhead.Id));
            Assert.Throws<System.InvalidOperationException>(() => traversal.MoveTo(twoLayersAhead.Id));
            Assert.AreEqual(map.StartNodeId, traversal.CurrentNodeId);
        }

        [Test]
        public void MoveTo_RejectsAStepBackwards()
        {
            var map = RegionMapGenerator.Generate(seed: 12);
            var traversal = new RegionMapTraversal(map);
            traversal.MoveTo(traversal.AvailableNextNodes[0].Id);

            Assert.IsFalse(traversal.CanMoveTo(map.StartNodeId));
            Assert.Throws<System.InvalidOperationException>(() => traversal.MoveTo(map.StartNodeId));
        }

        [Test]
        public void MoveTo_RejectsANodeOnTheSameLayer()
        {
            var map = RegionMapGenerator.Generate(seed: 13);
            var traversal = new RegionMapTraversal(map);
            var options = traversal.AvailableNextNodes.ToList();
            traversal.MoveTo(options[0].Id);

            Assert.IsFalse(traversal.CanMoveTo(options[1].Id), "sidestepping within a layer should not be walkable");
        }

        [Test]
        public void WalkingAnyBranch_AlwaysEndsAtTheGym_InExactlyOneStepPerLayer()
        {
            // Both extremes of the branch choice, across a spread of seeds: whichever way the
            // player leans, the map has to funnel them into the Gym and nowhere else.
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (bool takeLeftmost in new[] { true, false })
                {
                    var map = RegionMapGenerator.Generate(seed);
                    var traversal = new RegionMapTraversal(map);

                    int steps = 0;
                    while (!traversal.IsComplete)
                    {
                        var options = traversal.AvailableNextNodes;
                        Assert.IsNotEmpty(options, $"seed {seed}: dead end at {traversal.CurrentNodeId}");
                        traversal.MoveTo(takeLeftmost ? options.First().Id : options.Last().Id);
                        steps++;
                        Assert.LessOrEqual(steps, map.LayerCount, $"seed {seed}: walk did not terminate");
                    }

                    Assert.AreEqual(map.GymNodeId, traversal.CurrentNodeId, $"seed {seed}");
                    Assert.AreEqual(map.LayerCount - 1, steps, $"seed {seed}: one step per layer expected");
                    Assert.AreEqual(map.LayerCount, traversal.VisitedNodeIds.Count, $"seed {seed}");
                }
            }
        }

        [Test]
        public void TheWalkedPath_IsAContiguousChainOfRealEdges()
        {
            var map = RegionMapGenerator.Generate(seed: 21);
            var traversal = new RegionMapTraversal(map);
            while (!traversal.IsComplete)
            {
                traversal.MoveTo(traversal.AvailableNextNodes.Last().Id);
            }

            var visited = traversal.VisitedNodeIds;
            for (int i = 1; i < visited.Count; i++)
            {
                var previous = map.GetById(visited[i - 1]);
                CollectionAssert.Contains(previous.NextIds, visited[i], $"{visited[i - 1]} -> {visited[i]} is not a real edge");
            }
        }

        /// <summary>The defect this ownership move fixes: the walk used to be a field on
        /// RegionMapController, so opening the Team screen and coming back re-ran Start, generated
        /// a brand new random map, and put the player back at the beginning of it.</summary>
        [Test]
        public void ForRun_ResumesTheSameMapAndPosition_WhenTheScreenIsReEntered()
        {
            var run = new RunState();

            var first = RegionMapTraversal.ForRun(run, seed: 31, layerCount: RegionMapGenerator.DefaultLayerCount);
            var stepped = first.AvailableNextNodes.First().Id;
            first.MoveTo(stepped);
            Assert.AreEqual(stepped, first.CurrentNodeId);

            // Standing in for the screen being rebuilt from scratch on scene re-entry.
            var resumed = RegionMapTraversal.ForRun(run, seed: 999, layerCount: RegionMapGenerator.DefaultLayerCount);

            Assert.AreSame(first.Map, resumed.Map, "re-entering should not regenerate the map");
            Assert.AreEqual(31, resumed.Map.Seed, "a different seed must not be honoured for a map already in progress");
            Assert.AreEqual(stepped, resumed.CurrentNodeId, "re-entering should not reset the player's position");
            CollectionAssert.AreEqual(first.VisitedNodeIds, resumed.VisitedNodeIds);
        }

        [Test]
        public void ForRun_WritesTheWalkThroughToTheRun()
        {
            var run = new RunState();
            var traversal = RegionMapTraversal.ForRun(run, seed: 7, layerCount: RegionMapGenerator.DefaultLayerCount);

            traversal.MoveTo(traversal.AvailableNextNodes.First().Id);

            CollectionAssert.AreEqual(traversal.VisitedNodeIds, run.VisitedMapNodeIds,
                "the run owns the path; the traversal writes through to it");
            Assert.AreEqual(traversal.CurrentNodeId, run.VisitedMapNodeIds.Last());
        }

        [Test]
        public void RegenerateForRun_ReplacesTheMapAndRestartsTheWalk()
        {
            var run = new RunState();
            var first = RegionMapTraversal.ForRun(run, seed: 5, layerCount: RegionMapGenerator.DefaultLayerCount);
            first.MoveTo(first.AvailableNextNodes.First().Id);

            var fresh = RegionMapTraversal.RegenerateForRun(run, seed: 6, layerCount: RegionMapGenerator.DefaultLayerCount);

            Assert.AreEqual(6, fresh.Map.Seed);
            Assert.AreEqual(fresh.Map.StartNodeId, fresh.CurrentNodeId);
            Assert.AreEqual(1, run.VisitedMapNodeIds.Count);
            Assert.AreSame(run.LocationMap, fresh.Map);
        }

        [Test]
        public void ReachingTheGym_EndsTheWalk()
        {
            var map = RegionMapGenerator.Generate(seed: 22);
            var traversal = new RegionMapTraversal(map);
            while (!traversal.IsComplete)
            {
                traversal.MoveTo(traversal.AvailableNextNodes.First().Id);
            }

            Assert.IsTrue(traversal.IsComplete);
            Assert.IsEmpty(traversal.AvailableNextNodes);
            Assert.IsFalse(traversal.CanMoveTo(map.StartNodeId));
        }
    }
}
