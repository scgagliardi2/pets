using System.Collections.Generic;
using System.Linq;

namespace Pets.Meta
{
    /// <summary>A generated Location node-map (design doc §5): layered nodes joined by forward-only
    /// edges, from a single start node to a single mandatory Gym. Treat an instance as immutable
    /// once generated — the player's walk over it lives in RegionMapTraversal, not here.</summary>
    public sealed class RegionMap
    {
        public List<RegionMapNode> Nodes = new List<RegionMapNode>();
        public string StartNodeId;
        public string GymNodeId;
        public int LayerCount;

        /// <summary>The seed this map was generated from, kept so a map that looks wrong on screen
        /// can be reproduced exactly in a test.</summary>
        public int Seed;

        public RegionMapNode GetById(string id) => Nodes.FirstOrDefault(n => n.Id == id);

        public List<RegionMapNode> NodesInLayer(int layer) => Nodes.Where(n => n.Layer == layer).ToList();
    }
}
