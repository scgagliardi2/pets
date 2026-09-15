using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>The curved charge meter that sits over a mon's head: a shallow arc that fills as its
    /// charge builds and empties when its passive fires (design doc §10.2 — the meter fills at a
    /// rate set by Speed, triggers at the threshold, then resets to zero).
    ///
    /// Drawn as a generated mesh rather than an Image, because there is no arc sprite and the shape
    /// needs to be driven by numbers: sweep, thickness and radius are all tunable, and the fill has
    /// to end at an arbitrary angle. A radial-filled Image could do a circle segment, but only by
    /// first drawing an arc-shaped texture to fill, which is art we'd then have to redraw to change
    /// the sweep. Emitting the ring segment directly keeps the shape a parameter.
    ///
    /// Track and fill are one mesh, in two passes: the whole sweep in the track colour, then the
    /// filled portion over the top of it. One Graphic rather than two stacked Images means one draw
    /// call per mon and no chance of the two drifting out of alignment.</summary>
    public sealed class ChargeArcView : MaskableGraphic
    {
        [SerializeField, Range(8, 96)] private int segments = 40;
        [SerializeField] private float radius = 54f;
        [SerializeField] private float thickness = 9f;

        /// <summary>How wide the arc opens, in degrees, centred on straight up. Shallow by design —
        /// the mockup's arcs are a gentle curve over the head, not a halo.</summary>
        [SerializeField, Range(20f, 340f)] private float sweepDegrees = 110f;

        [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color fillColor = new Color(0.36f, 0.85f, 0.35f, 1f);

        [SerializeField, Range(0f, 1f)] private float fill;

        /// <summary>Charge as a fraction of the trigger threshold, 0 to 1. Setting it redraws.</summary>
        public float Fill
        {
            get => fill;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (!Mathf.Approximately(clamped, fill))
                {
                    fill = clamped;
                    SetVerticesDirty();
                }
            }
        }

        public Color FillColor
        {
            get => fillColor;
            set
            {
                fillColor = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            // Centred on straight up (90°) so the arc sits symmetrically over the mon's head, and
            // swept left-to-right so it fills the way the eye reads.
            float half = sweepDegrees * 0.5f;
            float startAngle = 90f + half;
            float endAngle = 90f - half;

            AddRibbon(vh, startAngle, endAngle, trackColor);
            if (fill > 0f)
            {
                float fillEnd = Mathf.Lerp(startAngle, endAngle, fill);
                AddRibbon(vh, startAngle, fillEnd, fillColor);
            }
        }

        /// <summary>Emits one arc band as a strip of quads between an inner and outer radius.
        /// Segment count is scaled by how much of the sweep is being drawn, so a nearly-empty fill
        /// doesn't spend the full budget on a sliver — but never drops below two, or a short fill
        /// would collapse to a straight line.</summary>
        private void AddRibbon(VertexHelper vh, float fromDegrees, float toDegrees, Color32 color)
        {
            float span = Mathf.Abs(toDegrees - fromDegrees);
            if (span <= 0.01f)
            {
                return;
            }

            int count = Mathf.Max(2, Mathf.CeilToInt(segments * (span / Mathf.Max(1f, sweepDegrees))));
            float inner = radius - thickness * 0.5f;
            float outer = radius + thickness * 0.5f;

            for (int i = 0; i <= count; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)count) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(dir * inner, color, Vector2.zero);
                vh.AddVert(dir * outer, color, Vector2.zero);
            }

            // Two verts per step, so the quad for step i is the pair at i and the pair at i+1.
            int baseIndex = vh.currentVertCount - (count + 1) * 2;
            for (int i = 0; i < count; i++)
            {
                int v = baseIndex + i * 2;
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
