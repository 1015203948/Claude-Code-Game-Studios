using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// 碰撞层位掩码。
    /// </summary>
    [Flags]
    public enum CollisionLayers
    {
        None = 0,
        Enemy = 1 << 0,
        Player = 1 << 1,
        Neutral = 1 << 2,
    }

    /// <summary>
    /// 伤害球检测器。
    /// 基于物理SphereCast的碰撞检测系统，用于技能和普攻的命中判定。
    /// 实现数据驱动的设计：所有参数从外部配置获取，不硬编码任何游戏数值。
    /// </summary>
    public class HitboxDetector
    {
        private readonly LayerMask _layerMask;
        private readonly int _maxHitsPerCast;
        private readonly float _skinThickness;

        // 内部状态 — 使用 RaycastHit[] 数组以适配 Physics.SphereCastNonAlloc
        // Buffer size matches _maxHitsPerCast to prevent overflow
        private readonly RaycastHit[] _hitBuffer;
        private readonly List<HitResult> _results = new List<HitResult>(8);

        /// <summary>
        /// 创建检测器实例。
        /// </summary>
        /// <param name="layerMask">检测层位掩码</param>
        /// <param name="maxHitsPerCast">每次投射最多命中数量</param>
        /// <param name="skinThickness">球体碰撞体 Skin Thickness（防止近距离穿透）</param>
        public HitboxDetector(LayerMask layerMask, int maxHitsPerCast = 8, float skinThickness = 0.02f)
        {
            _layerMask = layerMask;
            _maxHitsPerCast = maxHitsPerCast;
            _skinThickness = skinThickness;
            _hitBuffer = new RaycastHit[maxHitsPerCast];
        }

        /// <summary>
        /// 执行球体投射检测。
        /// </summary>
        /// <param name="origin">检测起始点（世界坐标）</param>
        /// <param name="direction">检测方向（归一化向量）</param>
        /// <param name="radius">球体半径</param>
        /// <param name="distance">检测距离</param>
        /// <param name="timestamp">当前游戏时间戳（秒）</param>
        /// <returns>命中结果列表</returns>
        public IReadOnlyList<HitResult> Cast(Vector3 origin, Vector3 direction, float radius, float distance, float timestamp)
        {
            _results.Clear();

            // 使用SphereCastNonAlloc进行碰撞检测（预分配数组，零GC）
            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _hitBuffer,
                distance,
                _layerMask,
                QueryTriggerInteraction.Ignore);

            if (count > 0)
            {
                // 按距离排序（由近到远）
                Array.Sort(_hitBuffer, 0, count, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

                // 限制最大命中数
                int limit = Mathf.Min(count, _maxHitsPerCast);
                float invCastDistance = 1f / distance;

                for (int i = 0; i < limit; i++)
                {
                    RaycastHit hit = _hitBuffer[i];
                    HitResult result = new HitResult
                    {
                        Target = hit.collider.gameObject,
                        Point = hit.point,
                        Normal = hit.normal,
                        Direction = direction,
                        Radius = radius,
                        Timestamp = timestamp
                    };
                    _results.Add(result);
                }
            }

            return _results;
        }

        /// <summary>
        /// 执行盒状球体投射（用于宽幅技能检测）。
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="halfExtents">半边长</param>
        /// <param name="direction">方向</param>
        /// <param name="orientation">旋转角度（弧度）</param>
        /// <param name="radius">球体半径（用于胶囊和球体碰撞体）</param>
        /// <param name="distance">投射距离</param>
        /// <param name="timestamp">时间戳</param>
        /// <returns>命中结果列表</returns>
        public IReadOnlyList<HitResult> CastBox(Vector3 center, Vector3 halfExtents, Vector3 direction, float orientation, float radius, float distance, float timestamp)
        {
            _results.Clear();

            Quaternion orientationQ = Quaternion.Euler(0f, orientation * Mathf.Rad2Deg, 0f);

            int count = Physics.SphereCastNonAlloc(
                center,
                radius,
                direction,
                _hitBuffer,
                distance,
                _layerMask,
                QueryTriggerInteraction.Ignore);

            if (count > 0)
            {
                Array.Sort(_hitBuffer, 0, count, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

                int limit = Mathf.Min(count, _maxHitsPerCast);

                for (int i = 0; i < limit; i++)
                {
                    RaycastHit hit = _hitBuffer[i];
                    // 额外检查命中点是否在盒子范围内
                    Vector3 localPoint = orientationQ * (hit.point - center);
                    if (Mathf.Abs(localPoint.x) <= halfExtents.x &&
                        Mathf.Abs(localPoint.y) <= halfExtents.y &&
                        Mathf.Abs(localPoint.z) <= halfExtents.z)
                    {
                        HitResult result = new HitResult
                        {
                            Target = hit.collider.gameObject,
                            Point = hit.point,
                            Normal = hit.normal,
                            Direction = direction,
                            Radius = radius,
                            Timestamp = timestamp
                        };
                        _results.Add(result);
                    }
                }
            }

            return _results;
        }

        /// <summary>
        /// 获取上次检测的结果（供调试和可视化使用）。
        /// </summary>
        public IReadOnlyList<HitResult> GetLastResults() => _results;
    }
}
