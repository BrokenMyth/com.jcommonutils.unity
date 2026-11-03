using UnityEngine;

namespace JCommonUtils.Geometry
{
    /// <summary>世界 XZ 平面的扇形判定；高度沿世界 Y，局部原点偏移应用锚点变换。</summary>
    public static class HorizontalSector
    {
        private const float MinimumRadius = 0.01f;
        private const float MinimumHalfHeight = 0.01f;
        private const float CenterDistance = 0.001f;
        private const float MinimumAngle = 1f;
        private const float MaximumAngle = 360f;
        private const float AngleTolerance = 0.01f;
        private const float MinimumForwardSqrMagnitude = 0.0001f;

        /// <summary>
        /// 判断世界坐标是否位于水平扇形内。半径和半高度为世界单位，不重复乘锚点缩放；
        /// angle 为完整张角（度），夹到 1–360 度并保留边缘容差。锚点近乎竖直朝向时使用世界前方。
        /// 复杂度 O(1)，无托管集合或临时对象分配；不读取场景对象、伤害、导航或网络状态。
        /// </summary>
        public static bool ContainsWorldPoint(Transform anchor, Vector3 localOriginOffset,
            float radius, float angle, bool useVerticalBand, float verticalHalfExtent, Vector3 worldPoint)
        {
            if (anchor == null) return false;

            Vector3 origin = anchor.TransformPoint(localOriginOffset);
            Vector3 toPoint = worldPoint - origin;
            if (useVerticalBand && Mathf.Abs(toPoint.y) > Mathf.Max(MinimumHalfHeight, verticalHalfExtent))
                return false;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(toPoint, Vector3.up);
            float distance = horizontalDirection.magnitude;
            if (distance > Mathf.Max(MinimumRadius, radius)) return false;
            if (distance <= CenterDistance) return true;

            return Vector3.Angle(GetHorizontalForward(anchor), horizontalDirection)
                   <= Mathf.Clamp(angle, MinimumAngle, MaximumAngle) * 0.5f + AngleTolerance;
        }

        /// <summary>取得偏移后的世界原点；锚点为空时将偏移直接视为世界坐标。</summary>
        public static Vector3 GetOrigin(Transform anchor, Vector3 localOriginOffset)
        {
            return anchor != null ? anchor.TransformPoint(localOriginOffset) : localOriginOffset;
        }

        /// <summary>将锚点前向投影到世界水平面，竖直朝向回退到世界前方。</summary>
        internal static Vector3 GetHorizontalForward(Transform anchor)
        {
            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            return forward.sqrMagnitude <= MinimumForwardSqrMagnitude ? Vector3.forward : forward.normalized;
        }
    }
}
