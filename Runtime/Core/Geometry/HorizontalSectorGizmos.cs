using UnityEngine;

namespace JCommonUtils.Geometry
{
    /// <summary>扇形调试绘制，与运行时几何判定分开；仅从 OnDrawGizmos 等有效 Gizmo 回调调用。</summary>
    public static class HorizontalSectorGizmos
    {
        private const float MinimumRadius = 0.01f;
        private const float DegreesPerSegment = 4f;
        private const int MinimumSegments = 8;
        private const int MaximumSegments = 90;

        /// <summary>
        /// 用径向线和边界线表示世界水平扇形，结束后恢复 Gizmos.color。
        /// 半径是世界单位；复杂度 O(S)，分段数 S 被限制为 8–90，不创建临时集合。
        /// </summary>
        public static void Draw(Transform anchor, Vector3 localOriginOffset, float radius,
            float angle, Color fillColor, Color wireColor)
        {
            if (anchor == null || radius <= MinimumRadius) return;
            Vector3 center = HorizontalSector.GetOrigin(anchor, localOriginOffset);
            Quaternion facing = Quaternion.LookRotation(HorizontalSector.GetHorizontalForward(anchor), Vector3.up);
            float clampedAngle = Mathf.Clamp(angle, 1f, 360f);
            float halfAngle = clampedAngle * 0.5f;
            int segments = Mathf.Clamp(Mathf.CeilToInt(clampedAngle / DegreesPerSegment), MinimumSegments, MaximumSegments);
            Color previousColor = Gizmos.color;
            try
            {
                DrawLines(center, facing, radius, halfAngle, segments, fillColor, wireColor);
            }
            finally
            {
                Gizmos.color = previousColor;
            }
        }

        // 两轮绘制保留原径向填充线和独立轮廓；每轮最多 90 段，坐标为栈上值类型。
        private static void DrawLines(Vector3 center, Quaternion facing, float radius, float halfAngle,
            int segments, Color fillColor, Color wireColor)
        {
            Vector3 left = PointOnArc(center, facing, radius, -halfAngle);
            Vector3 previous = left;
            Gizmos.color = fillColor;
            for (int index = 1; index <= segments; index++)
            {
                Vector3 next = PointOnArc(center, facing, radius, Mathf.Lerp(-halfAngle, halfAngle, index / (float)segments));
                Gizmos.DrawLine(center, previous);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.color = wireColor;
            Gizmos.DrawLine(center, left);
            Gizmos.DrawLine(center, PointOnArc(center, facing, radius, halfAngle));
            previous = left;
            for (int index = 1; index <= segments; index++)
            {
                Vector3 next = PointOnArc(center, facing, radius, Mathf.Lerp(-halfAngle, halfAngle, index / (float)segments));
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static Vector3 PointOnArc(Vector3 center, Quaternion facing, float radius, float angle)
        {
            return center + facing * Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
        }
    }
}
