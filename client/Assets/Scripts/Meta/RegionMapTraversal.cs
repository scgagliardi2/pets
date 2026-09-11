using System;
using System.Collections.Generic;
using System.Linq;

namespace Pets.Meta
{
    /// <summary>The player's walk across a generated RegionMap (design doc §5): where they stand,
    /// which nodes that opens up, and the path taken to get there. Kept separate from RegionMap so
    /// the graph stays an immutable generated artifact and only the walk mutates.
    ///
    /// Deliberately knows nothing about what a node *does* on arrival — stepping onto a PvE node
    /// doesn't start a fight here. Wiring node resolution (fight/event/camp/Gym) onto arrival is
    /// the next Phase 1 step (PLAN.md); this is the movement layer underneath it.</summary>
    public sealed class RegionMapTraversal
    {
        private readonly List<string> visitedNodeIds = new List<string>();

        public RegionMapTraversal(RegionMap map)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            CurrentNodeId = map.StartNodeId;
            visitedNodeIds.Add(CurrentNodeId);
        }

        public RegionMap Map { get; }

        public string CurrentNodeId { get; private set; }

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

            CurrentNodeId = nodeId;
            visitedNodeIds.Add(nodeId);
        }
    }
}
