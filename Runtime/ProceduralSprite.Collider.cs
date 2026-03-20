using UnityEngine;

namespace ProceduralShapes.Runtime
{
    public partial class ProceduralSprite
    {
        [Header("Physics")]
        [Tooltip("Автоматически генерировать PolygonCollider2D при изменении формы")]
        public bool m_AutoGenerateCollider = false;

        private void UpdateCollider()
        {
            if (!m_AutoGenerateCollider) return;

            PolygonCollider2D col = GetComponent<PolygonCollider2D>();
            if (col == null) col = gameObject.AddComponent<PolygonCollider2D>();

            int segments = 32;
            if (m_ShapeType == ShapeType.Polygon) segments = m_PolygonSides;
            else if (m_ShapeType == ShapeType.Star) segments = m_StarPoints * 2;
            else if (m_ShapeType == ShapeType.Triangle) segments = 3;

            Vector2[] points = new Vector2[segments];
            Rect rect = GetRect();
            float hw = rect.width * 0.5f * m_ShapeScale2D.x;
            float hh = rect.height * 0.5f * m_ShapeScale2D.y;
            float angleStep = 360f / segments;

            Vector2 offset = GetGeometricCenterOffset();

            for (int i = 0; i < segments; i++)
            {
                float angle = (i * angleStep) * Mathf.Deg2Rad;
                float r = 1f;
                if (m_ShapeType == ShapeType.Star && (i % 2 != 0)) r = m_StarRatio;

                points[i] = new Vector2(Mathf.Sin(angle) * hw * r, Mathf.Cos(angle) * hh * r) + offset;
            }

            col.points = points;
        }

        partial void OnShapeChangedCustom()
        {
            UpdateCollider();
        }
    }
}