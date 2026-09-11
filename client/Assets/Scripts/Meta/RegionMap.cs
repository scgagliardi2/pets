using System.Collections.Generic;
using System.Linq;

namespace Pets.Meta
{
    public sealed class RegionMap
    {
        public List<RegionMapNode> Nodes = new List<RegionMapNode>();
        public string StartNodeId;
        public int LayerCount;

        public RegionMapNode GetById(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    }
}
