using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Four-segment ring drawn with Painter2D. Segment order matches <see cref="RadialDirection"/>.
    /// The hovered segment is drawn larger and opaque; the equipped one gets an outline.
    /// </summary>
    [UxmlElement]
    public partial class RadialRing : VisualElement
    {
        public const int SegmentCount = 4;

        // Up, Right, Down, Left. Panel space has +y pointing down, so "up" is -90 degrees.
        static readonly float[] s_segmentCenterAngles = { -90f, 0f, 90f, 180f };

        readonly Color[] _colors = { Color.gray, Color.gray, Color.gray, Color.gray };
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
                bool hovered = i == _hoveredIndex;
                float segmentOuter = hovered ? outer + HoverGrowth : outer;
                float start = s_segmentCenterAngles[i] - halfSpan;
                float end = s_segmentCenterAngles[i] + halfSpan;

                painter.BeginPath();
                painter.Arc(center, segmentOuter, Angle.Degrees(start), Angle.Degrees(end), ArcDirection.Clockwise);
                painter.Arc(center, inner, Angle.Degrees(end), Angle.Degrees(start), ArcDirection.CounterClockwise);
                painter.ClosePath();

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
