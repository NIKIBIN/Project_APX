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
            if (rect.width < 1f || rect.height < 1f || _radius >= GetOpenRadius(_center))
                return;

            Painter2D painter = context.painter2D;
            painter.fillColor = _color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();

            // A second sub-path filled with the odd-even rule punches the hole.
            if (_radius > 0.5f)
            {
                painter.MoveTo(_center + new Vector2(_radius, 0f));
                painter.Arc(_center, _radius, Angle.Degrees(0f), Angle.Degrees(180f));
                painter.Arc(_center, _radius, Angle.Degrees(180f), Angle.Degrees(360f));
                painter.ClosePath();
            }

            painter.Fill(FillRule.OddEven);
        }
    }
}
