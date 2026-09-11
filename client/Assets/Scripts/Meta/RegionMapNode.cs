using System.Collections.Generic;

namespace Pets.Meta
{
    /// <summary>One node in a generated branching Region/Location map (design doc §5). Visual
    /// prototype only — see RegionMapGenerator.</summary>
    public sealed class RegionMapNode
    {
        public string Id;
        public NodeType Type;
        public int Layer;
        public int IndexInLayer;

        /// <summary>Ids of nodes in the next layer this node connects forward to.</summary>
        public List<string> NextIds = new List<string>();
    }
}
