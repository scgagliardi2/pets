namespace Pets.Meta
{
    /// <summary>One node in a Location's node-map (design doc §5.1). Phase 0's map is a simple
    /// linear sequence rather than the design doc's branching graph — see ForestLocationFactory.</summary>
    public sealed class LocationNodeState
    {
        public string Id;
        public NodeType Type;
        public bool Cleared;
    }
}
