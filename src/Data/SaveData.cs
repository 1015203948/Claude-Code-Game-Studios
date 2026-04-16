using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data {
    /// <summary>
    /// Root save data structure for game serialization.
    /// Contains all runtime state that must persist across save/load.
    ///
    /// Serialization: JSON (JsonUtility) or binary.
    /// File location: Application.persistentDataPath + "/saves/save_{timestamp}.json"
    /// </summary>
    [Serializable]
    public struct SaveData {
        /// <summary>ID of the currently active ship (the player's flagship).</summary>
        public string ActiveShipId;

        /// <summary>
        /// Simulation rate at time of save.
        /// Valid values: 0, 1, 5, 20. Invalid values on load → default to 1.
        /// </summary>
        public float SimRate;

        /// <summary>All ship runtime states.</summary>
        public List<ShipSaveData> Ships;

        /// <summary>All star map node runtime states.</summary>
        public List<NodeSaveData> Nodes;

        /// <summary>All fleet dispatch orders.</summary>
        public List<DispatchSaveData> Dispatches;

        /// <summary>Save timestamp (ISO 8601 string).</summary>
        public string Timestamp;
    }

    /// <summary>
    /// Per-ship runtime state for save/load.
    /// </summary>
    [Serializable]
    public struct ShipSaveData {
        public string InstanceId;
        public string BlueprintId;
        public float CurrentHull;
        public ShipState State;
        public Vector3Save Position;
        public QuaternionSave Rotation;
        public string DockedNodeId; // nullable
        public string CarrierInstanceId; // nullable, carrier ships only
    }

    /// <summary>
    /// Serializable Vector3 wrapper (JsonUtility cannot serialize Vector3 directly in structs).
    /// </summary>
    [Serializable]
    public struct Vector3Save {
        public float x, y, z;
        public Vector3Save(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
        public static Vector3Save FromVector3(Vector3 v) => new Vector3Save(v.x, v.y, v.z);
    }

    /// <summary>
    /// Serializable Quaternion wrapper (JsonUtility cannot serialize Quaternion directly in structs).
    /// </summary>
    [Serializable]
    public struct QuaternionSave {
        public float x, y, z, w;
        public QuaternionSave(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
        public static QuaternionSave FromQuaternion(Quaternion q) => new QuaternionSave(q.x, q.y, q.z, q.w);
    }

    /// <summary>
    /// Per-node runtime state for save/load.
    /// </summary>
    [Serializable]
    public struct NodeSaveData {
        public string NodeId;
        public OwnershipState Ownership;
        public FogState FogState;
        public string DockedFleetId; // nullable
    }

    /// <summary>
    /// Fleet dispatch order for save/load.
    /// </summary>
    [Serializable]
    public struct DispatchSaveData {
        public string FleetId;
        public string FromNodeId;
        public string ToNodeId;
        public float ElapsedSeconds;
        public float TotalSeconds;
    }
}
