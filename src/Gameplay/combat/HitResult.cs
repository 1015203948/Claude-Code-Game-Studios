using System;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// 碰撞检测结果数据类。
    /// 描述一次碰撞事件的完整信息。
    /// </summary>
    [Serializable]
    public struct HitResult
    {
        /// <summary>被击中的目标GameObject</summary>
        public GameObject Target;
        /// <summary>碰撞点世界坐标</summary>
        public Vector3 Point;
        /// <summary>碰撞表面法线</summary>
        public Vector3 Normal;
        /// <summary>碰撞方向（从攻击者指向目标）</summary>
        public Vector3 Direction;
        /// <summary>命中的伤害球半径</summary>
        public float Radius;
        /// <summary>命中时间戳（秒）</summary>
        public float Timestamp;
    }
}