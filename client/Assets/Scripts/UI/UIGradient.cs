using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// Paints a vertical top-to-bottom color gradient onto whatever Graphic it's attached to
    /// (typically a full-screen background Image) via a per-vertex color blend — no gradient
    /// texture asset needed. This is how screens get distinguishable "scenery" during the
    /// placeholder-art phase without any imported art (PLAN.md §8). The Graphic's own color
    /// should stay white; TopColor/BottomColor (including alpha, for translucent overlays) are
    /// what actually gets rendered.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class UIGradient : BaseMeshEffect
    {
        public Color TopColor = Color.white;
        public Color BottomColor = Color.black;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            var vertex = default(UIVertex);
            int count = vh.currentVertCount;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
            }

            float range = Mathf.Max(maxY - minY, 0.0001f);
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = (vertex.position.y - minY) / range;
                vertex.color = Color.Lerp(BottomColor, TopColor, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
