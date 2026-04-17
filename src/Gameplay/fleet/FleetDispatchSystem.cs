using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;

namespace Game.Gameplay.Fleet {
    /// <summary>
    /// Fleet dispatch lifecycle manager for StarMapScene.
    /// Manages DispatchOrder creation, transit progress, and unattended combat resolution.
    /// Implements ADR-0017 Fleet Dispatch Architecture.
    /// Story 012: RequestDispatch creation + ShipState IN_TRANSIT transition.
    /// Story 007: Unattended combat U-4 resolution (ResolveUnattendedCombat).
    /// Story 013: Transit hop advancement via SimClock.DeltaTime.
    /// Story 014: CancelDispatch return journey (IsReturning, CloseOrder).
    /// Story 015: Arrival routing (ENEMY→Combat/U-4, NEUTRAL/PLAYER→DOCKED), OnShipDestroyed cleanup.
    /// </summary>
    public class FleetDispatchSystem : MonoBehaviour
    {
        public static FleetDispatchSystem Instance { get; private set; }

        // ─────────────────────────────────────────────────────────────────
        // DispatchOrder Registry
        // ─────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, DispatchOrder> _orders = new Dictionary<string, DispatchOrder>();

        // ─────────────────────────────────────────────────────────────────
        // Events
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Broadcasts when a new dispatch order is created.</summary>
        public event Action<DispatchOrder> OnDispatchCreated;

        /// <summary>Broadcasts when unattended combat results in victory.</summary>
        public event Action<string> OnUnattendedVictory; // arg: nodeId

        /// <summary>Broadcasts when unattended combat results in defeat.</summary>
        public event Action<string> OnUnattendedDefeat; // arg: nodeId

        /// <summary>Broadcasts when a ship is destroyed while having an active dispatch order (orphaned order cleanup).</summary>
        public event Action<string> OnShipDestroyed; // arg: shipId

        /// <summary>Broadcasts when a dispatch order is closed (arrived, cancelled, or timed out).</summary>
        public event Action<string> OnOrderClosed; // arg: orderId

        // ─────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Fixed enemy fleet size for unattended combat (MVP).</summary>
        private const int ENEMY_FLEET_SIZE = 2;

        /// <summary>Travel time per hop in seconds (Story 013).</summary>
        public const float FLEET_TRAVEL_TIME = 3.0f;

        // ─────────────────────────────────────────────────────────────────
        // Singleton
        // ─────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ─────────────────────────────────────────────────────────────────
        // Transit Hop Advancement (Story 013)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Advances all active dispatch orders by SimClock.DeltaTime each frame.
        /// O(order_count) — iterates all orders each frame.
        /// Skips advancement when SimRate = 0 (paused).
        /// </summary>
        private void Update()
        {
            // Guard: SimClock must be available
            var simClock = global::Gameplay.SimClock.Instance;
            if (simClock == null) return;

            float simDelta = simClock.DeltaTime;
            if (simDelta <= 0f) return; // SimRate = 0: no advancement

            // Snapshot to avoid modifying collection during iteration
            var orders = _orders.Values.ToList();
            foreach (var order in orders) {
                AdvanceOrder(order, simDelta);
            }
        }

        /// <summary>
        /// Advances a single dispatch order by delta seconds.
        /// Handles both forward transit and return journey.
        /// </summary>
        private void AdvanceOrder(DispatchOrder order, float delta)
        {
            if (order.IsReturning) {
                AdvanceReturn(order, delta);
                return;
            }

            order.HopProgress += delta;

            // Handle multiple hops in one frame (if delta > FLEET_TRAVEL_TIME at high SimRate)
            while (order.HopProgress >= FLEET_TRAVEL_TIME) {
                order.HopProgress -= FLEET_TRAVEL_TIME;
                order.CurrentHopIndex++;

                if (order.CurrentHopIndex >= order.LockedPath.Count - 1) {
                    ArrivedAtDestination(order);
                    return;
                }
            }
        }

        /// <summary>
        /// Advances a return journey order (Story 014: CancelDispatch).
        /// Ship travels backward along the reversed path: LockedPath.Take(originalIndex+1).Reverse().
        /// CurrentHopIndex increments as it advances forward through the reversed path.
        /// When it reaches the end (origin node), ship becomes DOCKED and order is closed.
        /// </summary>
        private void AdvanceReturn(DispatchOrder order, float delta)
        {
            order.HopProgress += delta;

            while (order.HopProgress >= FLEET_TRAVEL_TIME) {
                order.HopProgress -= FLEET_TRAVEL_TIME;
                order.CurrentHopIndex++;

                if (order.CurrentHopIndex >= order.LockedPath.Count - 1) {
                    // Arrived back at origin node
                    var ship = GameDataManager.Instance?.GetShip(order.ShipId);
                    if (ship != null) {
                        ship.SetState(ShipState.DOCKED);
                    }
                    CloseOrder(order);
                    return;
                }
            }
        }

        /// <summary>
        /// Called when a dispatch order reaches its destination node (forward journey).
        /// Story 015: routes to combat, unattended combat, or docking based on node type.
        /// </summary>
        private void ArrivedAtDestination(DispatchOrder order)
        {
            string arrivalNodeId = order.LockedPath[^1];
            var ship = GameDataManager.Instance?.GetShip(order.ShipId);

            // EC-8: Ship already destroyed (e.g., U-4 path already destroyed it) → clean up
            if (ship == null || ship.State == ShipState.DESTROYED) {
                Debug.Log($"[FleetDispatch] Order {order.OrderId}: ship already destroyed — cleaning orphaned order");
                CloseOrder(order);
                return;
            }

            // Update ship's docked node
            ship.DockedNodeId = arrivalNodeId;

            // Look up node type
            var node = GetNodeById(arrivalNodeId);

            if (node != null && node.NodeType == NodeType.ENEMY) {
                if (ship.IsPlayerControlled) {
                    // Player in cockpit → cockpit combat (Story 004 integration)
                    Debug.Log($"[FleetDispatch] Order {order.OrderId}: ENEMY node, player controlled → BeginCombat");
                    CombatSystem.Instance?.BeginCombat(order.ShipId, arrivalNodeId);
                } else {
                    // NPC ship → unattended combat U-4 path (Story 007)
                    Debug.Log($"[FleetDispatch] Order {order.OrderId}: ENEMY node, NPC → UnattendedCombat");
                    ResolveUnattendedCombat(order.ShipId, arrivalNodeId);
                }
            } else {
                // NEUTRAL or PLAYER node → dock
                ship.SetState(ShipState.DOCKED);
                Debug.Log($"[FleetDispatch] Order {order.OrderId}: {node?.NodeType} node → DOCKED");
                StarMapSystem.OnFleetArrived(order.ShipId, arrivalNodeId);
            }

            CloseOrder(order);
        }

        /// <summary>
        /// Gets a StarNode by its ID from StarMapData.
        /// </summary>
        private StarNode GetNodeById(string nodeId)
        {
            var map = GameDataManager.Instance?.GetStarMapData();
            if (map == null) return null;
            foreach (var node in map.Nodes) {
                if (node.Id == nodeId) return node;
            }
            return null;
        }

        /// <summary>
        /// Removes a completed order from the registry.
        /// Idempotent: calling twice is safe (second call is a no-op).
        /// </summary>
        private void CloseOrder(DispatchOrder order)
        {
            if (!_orders.Remove(order.OrderId)) {
                Debug.Log($"[FleetDispatch] Order {order.OrderId} was already closed — no-op");
                return;
            }
            Debug.Log($"[FleetDispatch] Order {order.OrderId} closed");
            OnOrderClosed?.Invoke(order.OrderId);
        }

        // ─────────────────────────────────────────────────────────────────
        // CancelDispatch (Story 014)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels an in-transit dispatch order and initiates a return journey to the origin node.
        /// The ship will travel backward along the path it has already traveled.
        ///
        /// AC: LockedPath.Take(CurrentHopIndex+1).Reverse() gives the return path.
        /// ShipState remains IN_TRANSIT during return; becomes DOCKED on arrival.
        /// </summary>
        public void CancelDispatch(string shipId)
        {
            // Find the active order for this ship
            var order = _orders.Values.FirstOrDefault(o => o.ShipId == shipId);
            if (order == null) {
                Debug.LogWarning($"[FleetDispatch] CancelDispatch: no active order found for {shipId}");
                return;
            }

            if (order.IsReturning) {
                Debug.LogWarning($"[FleetDispatch] CancelDispatch: order for {shipId} is already returning");
                return;
            }

            // Compute reverse path: nodes from origin up to and including current position, reversed
            // Take(CurrentHopIndex + 1) gives us the nodes from start up to current node
            // Reverse() gives us the path back to origin
            var returnPath = order.LockedPath.Take(order.CurrentHopIndex + 1).Reverse().ToList();

            order.LockedPath = returnPath;
            order.CurrentHopIndex = 0;
            order.HopProgress = 0f;
            order.IsReturning = true;

            Debug.Log($"[FleetDispatch] Order {order.OrderId} returning: [{string.Join(", ", returnPath)}]");
        }

        // ─────────────────────────────────────────────────────────────────
        // RequestDispatch
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a dispatch order for a ship to travel to a destination node.
        /// Validates ship is DOCKED, computes path, creates order, transitions ship to IN_TRANSIT.
        /// Returns null if validation fails or no path exists.
        /// </summary>
        public DispatchOrder RequestDispatch(string shipId, string destinationNodeId)
        {
            // 1. Validate ship state
            var ship = GameDataManager.Instance?.GetShip(shipId);
            if (ship == null || ship.State != ShipState.DOCKED) {
                Debug.LogWarning($"[FleetDispatch] {shipId} is not DOCKED — cannot dispatch.");
                return null;
            }

            // 2. Path computation (BFS)
            string originNodeId = ship.DockedNodeId;
            if (string.IsNullOrEmpty(originNodeId)) {
                Debug.LogWarning($"[FleetDispatch] {shipId} has no DockedNodeId — cannot dispatch.");
                return null;
            }

            var map = GameDataManager.Instance?.GetStarMapData();
            if (map == null) {
                Debug.LogWarning($"[FleetDispatch] StarMapData not available — cannot dispatch.");
                return null;
            }

            var path = StarMapPathfinder.FindPath(map, originNodeId, destinationNodeId);
            if (path == null || path.Count < 1) {
                Debug.LogWarning($"[FleetDispatch] No path found from {originNodeId} to {destinationNodeId}.");
                return null;
            }

            // 3. Create DispatchOrder (path snapshot)
            var order = new DispatchOrder {
                OrderId = $"order_{Guid.NewGuid():N}",
                ShipId = shipId,
                OriginNodeId = originNodeId,
                DestinationNodeId = destinationNodeId,
                LockedPath = new List<string>(path), // snapshot — not affected by subsequent changes
                CurrentHopIndex = 0,
                HopProgress = 0f,
                IsReturning = false,
                Timestamp = Time.time,
            };

            _orders[order.OrderId] = order;

            // 4. Transition ship state to IN_TRANSIT
            ship.SetState(ShipState.IN_TRANSIT);

            // 5. Broadcast
            OnDispatchCreated?.Invoke(order);

            Debug.Log($"[FleetDispatch] Dispatch order {order.OrderId} created: {shipId} → {destinationNodeId}");
            return order;
        }

        // ─────────────────────────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets a dispatch order by ID.
        /// </summary>
        public DispatchOrder GetOrder(string orderId)
        {
            return _orders.TryGetValue(orderId, out var order) ? order : null;
        }

        /// <summary>
        /// Gets all active dispatch orders for a given ship.
        /// </summary>
        public IEnumerable<DispatchOrder> GetOrdersForShip(string shipId)
        {
            foreach (var order in _orders.Values) {
                if (order.ShipId == shipId) {
                    yield return order;
                }
            }
        }

        /// <summary>
        /// Gets all active dispatch orders (for StarMapUI edge highlighting).
        /// </summary>
        public IEnumerable<DispatchOrder> GetAllOrders()
            => _orders.Values;
        {
            foreach (var order in _orders.Values) {
                if (order.ShipId == shipId) {
                    yield return order;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Unattended Combat Resolution (U-4 path)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves unattended combat at a node when the player fleet arrives.
        /// Uses simplified P vs E model: each side loses 1 ship per exchange.
        /// Victory: E=0 and P>0. Defeat: P=0 (including tie P=E).
        ///
        /// AC: P=3, E=2 → VICTORY (P=1, E=0)
        /// AC: P=1, E=3 → DEFEAT (P=0)
        /// AC: P=1, E=1 → DEFEAT (tie)
        ///
        /// U-4 path: directly calls ShipDataModel.Destroy(), bypasses HealthSystem.
        /// </summary>
        public void ResolveUnattendedCombat(string shipId, string nodeId)
        {
            // Count player ships currently in DOCKED state at the destination node
            int P = GetPlayerShipsOnNode(nodeId);
            int E = ENEMY_FLEET_SIZE; // MVP fixed at 2

            Debug.Log($"[FleetDispatch] Unattended combat at {nodeId}: P={P}, E={E}");

            // Simplified combat loop: each exchange loses 1 ship on each side
            while (P > 0 && E > 0) {
                P -= 1;
                E -= 1;
            }

            if (E <= 0 && P > 0) {
                // Victory: all enemies destroyed, player fleet survives
                ResolveUnattendedVictory(nodeId);
            } else {
                // Defeat: all player ships destroyed (U-4 path — bypass HealthSystem)
                ResolveUnattendedDefeat(nodeId);
            }
        }

        private void ResolveUnattendedVictory(string nodeId)
        {
            Debug.Log($"[FleetDispatch] Unattended VICTORY at {nodeId}");
            OnUnattendedVictory?.Invoke(nodeId);
        }

        private void ResolveUnattendedDefeat(string nodeId)
        {
            Debug.Log($"[FleetDispatch] Unattended DEFEAT at {nodeId}");

            // U-4 path: directly destroy all player ships at this node
            // Bypasses HealthSystem — does NOT trigger OnShipDying
            if (GameDataManager.Instance != null) {
                foreach (var ship in GameDataManager.Instance.AllShips) {
                    if (ship.DockedNodeId == nodeId && ship.State == ShipState.DOCKED) {
                        ship.Destroy(); // U-4: bypass HealthSystem
                        OnShipDestroyed?.Invoke(ship.InstanceId); // clean up orphaned order
                    }
                }
            }

            OnUnattendedDefeat?.Invoke(nodeId);
        }

        /// <summary>
        /// Counts the number of player-controlled ships currently docked at a node.
        /// </summary>
        private int GetPlayerShipsOnNode(string nodeId)
        {
            if (GameDataManager.Instance == null) return 0;

            int count = 0;
            foreach (var ship in GameDataManager.Instance.AllShips) {
                if (ship.IsPlayerControlled
                    && ship.DockedNodeId == nodeId
                    && ship.State == ShipState.DOCKED) {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Represents a single fleet dispatch order.
    /// LockedPath is a snapshot — it is NOT affected by subsequent StarMap changes.
    /// </summary>
    [Serializable]
    public class DispatchOrder
    {
        /// <summary>Unique order ID.</summary>
        public string OrderId;

        /// <summary>The ship assigned to this order.</summary>
        public string ShipId;

        /// <summary>Node the ship departed from.</summary>
        public string OriginNodeId;

        /// <summary>Node the ship is traveling to.</summary>
        public string DestinationNodeId;

        /// <summary>
        /// Snapshot of the path at dispatch time.
        /// NOT affected by StarMap changes after dispatch.
        /// </summary>
        public List<string> LockedPath;

        /// <summary>
        /// Current hop index in LockedPath.
        /// 0 = just departed from OriginNodeId.
        /// </summary>
        public int CurrentHopIndex;

        /// <summary>
        /// Accumulated time (seconds) in the current hop.
        /// </summary>
        public float HopProgress;

        /// <summary>
        /// True if this order is a return journey (canceled and returning to origin).
        /// </summary>
        public bool IsReturning;

        /// <summary>
        /// Time when this order was created (Time.time at creation).
        /// </summary>
        public float Timestamp;
    }
}
