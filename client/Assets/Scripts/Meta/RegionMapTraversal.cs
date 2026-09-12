using System;
using System.Collections.Generic;
using System.Linq;

namespace Pets.Meta
{
    /// <summary>The player's walk across a generated RegionMap (design doc §5): where they stand,
    /// which nodes that opens up, and the path taken to get there. Kept separate from RegionMap so
    /// the graph stays an immutable generated artifact and only the walk mutates.
    ///
    /// The walk itself belongs to the run, not to this object — see <see cref="ForRun"/>. It used
    /// to be a field on RegionMapController, which meant leaving the map scene for the Team screen
    /// and coming back generated a brand new random map and put the player back at the start.
    ///
    /// Deliberately knows nothing about what a node *does* on arrival — stepping onto a PvE node
    /// doesn't start a fight here. Wiring node resolution (fight/event/camp/Gym) onto arrival is
    /// the next Phase 1 step (PLAN.md); this is the movement layer underneath it.</summary>
    public sealed class RegionMapTraversal
    {
        private readonly List<string> visitedNodeIds;

        /// <summary>A fresh walk, standing on the map's start node.</summary>
        public RegionMapTraversal(RegionMap map)
            : this(map, new List<string>())
        {
        }

        /// <summary>Binds to an existing path. The list is kept by reference, not copied, so a
        /// caller that owns it (RunState) sees every MoveTo — which is what lets the walk survive
        /// leaving and re-entering the map scene: the controller becomes a view over run state
        /// rather than the owner of it. An empty list starts the walk at the map's start node.</summary>
        public RegionMapTraversal(RegionMap map, List<string> visitedNodeIds)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            this.visitedNodeIds = visitedNodeIds ?? throw new ArgumentNullException(nameof(visitedNodeIds));
            if (this.visitedNodeIds.Count == 0)
            {
                this.visitedNodeIds.Add(map.StartNodeId);
            }
        }

        /// <summary>Binds a traversal to the run's own map and walked path, generating and storing
        /// a map first if the run hasn't got one yet. The run is the owner; this is the view.</summary>
        public static RegionMapTraversal ForRun(RunState run, int seed, int layerCount)
        {
            if (run.LocationMap == null)
            {
                run.LocationMap = RegionMapGenerator.Generate(seed, layerCount);
                run.VisitedMapNodeIds.Clear();
            }
            return new RegionMapTraversal(run.LocationMap, run.VisitedMapNodeIds);
        }

        /// <summary>Replaces the run's map with a freshly generated one and restarts the walk.
        /// The Region Map's "New Map" button, which is a prototyping affordance rather than
        /// something a real run should offer (PLAN.md §6).</summary>
        public static RegionMapTraversal RegenerateForRun(RunState run, int seed, int layerCount)
        {
            run.LocationMap = null;
            return ForRun(run, seed, layerCount);
        }

        public RegionMap Map { get; }

        /// <summary>Derived from the path rather than tracked alongside it: the walk is
        /// forward-only and always appends, so where the player stands is simply the last node
        /// visited. One piece of state, so position and path can't disagree.</summary>
        public string CurrentNodeId => visitedNodeIds[visitedNodeIds.Count - 1];

        public RegionMapNode CurrentNode => Map.GetById(CurrentNodeId);

        /// <summary>The path walked so far, oldest first — always starting at the start node.</summary>
        public IReadOnlyList<string> VisitedNodeIds => visitedNodeIds;

        /// <summary>The nodes the player may step onto from where they stand. Empty once the Gym is
        /// reached, since the Gym is every path's terminus.</summary>
        public IReadOnlyList<RegionMapNode> AvailableNextNodes =>
            CurrentNode.NextIds.Select(Map.GetById).ToList();

        /// <summary>True once the player is standing on the Gym — the end of the Location's map.</summary>
        public bool IsComplete => CurrentNode.Type == NodeType.Gym;

        public bool HasVisited(string nodeId) => visitedNodeIds.Contains(nodeId);

        /// <summary>Only forward edges out of the current node are walkable — there's no going back
        /// and no jumping to an unconnected node on the same layer.</summary>
        public bool CanMoveTo(string nodeId) => CurrentNode.NextIds.Contains(nodeId);

        public void MoveTo(string nodeId)
        {
            if (!CanMoveTo(nodeId))
            {
                throw new InvalidOperationException($"'{nodeId}' is not connected to the current node '{CurrentNodeId}'.");
            }

            visitedNodeIds.Add(nodeId);
        }
    }
}
