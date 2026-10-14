using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Four-segment ring drawn with Painter2D. Segment order matches <see cref="RadialDirection"/>.
    /// The hovered segment is drawn larger and opaque; the equipped one gets an outline. Locked segments are
    /// a faint grey silhouette that never grows.
    /// </summary>
    [UxmlElement]
    public partial class RadialRing : VisualElement
    {
        public const int SegmentCount = 4;

        // Up, Right, Down, Left. Panel space has +y pointing down, so "up" is -90 degrees.
        static readonly float[] s_segmentCenterAngles = { -90f, 0f, 90f, 180f };

        static readonly Color s_lockedFill = new(0.55f, 0.57f, 0.65f, 0.14f);
        static readonly Color s_lockedOutline = new(1f, 1f, 1f, 0.22f);

        readonly Color[] _colors = { Color.gray, Color.gray, Color.gray, Color.gray };
        readonly bool[] _locked = new bool[SegmentCount];
        int _hoveredIndex = -1;
        int _equippedIndex = -1;
        float _thickness = 110f;
        float _gapDegrees = 6f;

        public RadialRing()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        [UxmlAttribute]
        public float Thickness
        {
            get => _thickness;
            set { _thickness = value; MarkDirtyRepaint(); }
        }

        [UxmlAttribute]
        public float GapDegrees
        {
            get => _gapDegrees;
            set { _gapDegrees = value; MarkDirtyRepaint(); }
        }

        public int HoveredIndex
        {
            get => _hoveredIndex;
            set { if (_hoveredIndex != value) { _hoveredIndex = value; MarkDirtyRepaint(); } }
        }

        public int EquippedIndex
        {
            get => _equippedIndex;
            set { if (_equippedIndex != value) { _equippedIndex = value; MarkDirtyRepaint(); } }
        }

        /// <summary>Distance from the centre to the middle of the ring, in local pixels (for placing labels).</summary>
        public float MidRadius => OuterRadius - _thickness * 0.5f;

        float OuterRadius => Mathf.Max(0f, Mathf.Min(contentRect.width, contentRect.height) * 0.5f - HoverGrowth);

        const float HoverGrowth = 10f;

        public void SetSegmentColor(int index, Color color)
        {
            _colors[index] = color;
            MarkDirtyRepaint();
        }

        public void SetSegmentLocked(int index, bool locked)
        {
            if (_locked[index] == locked)
                return;

            _locked[index] = locked;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (contentRect.width < 1f || contentRect.height < 1f)
                return;

            Painter2D painter = context.painter2D;
            Vector2 center = contentRect.center;
            float outer = OuterRadius;
            float inner = Mathf.Max(0f, outer - _thickness);
            float halfSpan = 45f - _gapDegrees * 0.5f;

            for (int i = 0; i < SegmentCount; i++)
            {
                bool locked = _locked[i];
                bool hovered = i == _hoveredIndex;
                float segmentOuter = hovered && !locked ? outer + HoverGrowth : outer;
                float start = s_segmentCenterAngles[i] - halfSpan;
                float end = s_segmentCenterAngles[i] + halfSpan;

                painter.BeginPath();
                painter.Arc(center, segmentOuter, Angle.Degrees(start), Angle.Degrees(end), ArcDirection.Clockwise);
                painter.Arc(center, inner, Angle.Degrees(end), Angle.Degrees(start), ArcDirection.CounterClockwise);
                painter.ClosePath();

                if (locked)
                {
                    Color lockedFill = s_lockedFill;
                    if (hovered)
                        lockedFill.a *= 1.8f;
                    painter.fillColor = lockedFill;
                    painter.Fill();
                    painter.strokeColor = s_lockedOutline;
                    painter.lineWidth = 2f;
                    painter.lineJoin = LineJoin.Round;
                    painter.Stroke();
                    continue;
                }

                Color fill = _colors[i];
                fill.a = hovered ? 0.95f : 0.5f;
                painter.fillColor = fill;
                painter.Fill();

                if (i == _equippedIndex)
                {
                    painter.strokeColor = Color.white;
                    painter.lineWidth = 4f;
                    painter.lineJoin = LineJoin.Round;
                    painter.Stroke();
                }
            }
        }
    }
}
