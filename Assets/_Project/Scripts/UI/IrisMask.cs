using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Full-screen cover with a circular hole, drawn with Painter2D. A radius of 0 covers everything;
    /// a radius past <see cref="GetOpenRadius"/> draws nothing.
    /// </summary>
    [UxmlElement]
    public partial class IrisMask : VisualElement
    {
        Color _color = Color.black;
        Vector2 _center;
        float _radius = float.PositiveInfinity;

        public IrisMask()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        [UxmlAttribute]
        public Color Color
        {
            get => _color;
            set { _color = value; MarkDirtyRepaint(); }
        }

        /// <summary>Hole radius in local pixels; <see cref="float.PositiveInfinity"/> when fully open.</summary>
        public float Radius => _radius;

        public void SetHole(Vector2 center, float radius)
        {
            _center = center;
            _radius = Mathf.Max(0f, radius);
            MarkDirtyRepaint();
        }

        /// <summary>Smallest radius around <paramref name="center"/> that uncovers the whole element.</summary>
        public float GetOpenRadius(Vector2 center)
        {
            Rect rect = contentRect;
            float dx = Mathf.Max(center.x - rect.xMin, rect.xMax - center.x);
            float dy = Mathf.Max(center.y - rect.yMin, rect.yMax - center.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (!(rect.width >= 1f && rect.height >= 1f) || _radius >= GetOpenRadius(_center))
                return;

            // The cover is a circle around the hole, big enough to reach every corner, rather than the element's
            // rectangle: a hole crossing the rectangle's edge (centre near the screen edge) tessellates into an
            // eye-like shape, while two concentric circles never intersect.
            // A hole set before the first layout (e.g. covering the screen on load) has no valid centre yet.
            Vector2 center = float.IsFinite(_center.x) && float.IsFinite(_center.y) ? _center : rect.center;

            Painter2D painter = context.painter2D;
            painter.fillColor = _color;
            painter.BeginPath();
            AddCircle(painter, center, GetOpenRadius(center) + 2f);

            // A second sub-path filled with the odd-even rule punches the hole.
            if (_radius > 0.5f)
                AddCircle(painter, center, _radius);

            painter.Fill(FillRule.OddEven);
        }

        static void AddCircle(Painter2D painter, Vector2 center, float radius)
        {
            painter.MoveTo(center + new Vector2(radius, 0f));
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(180f));
            painter.Arc(center, radius, Angle.Degrees(180f), Angle.Degrees(360f));
            painter.ClosePath();
        }
    }
}
