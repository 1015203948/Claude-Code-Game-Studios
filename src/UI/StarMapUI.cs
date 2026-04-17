using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIEilite;
using Game.Channels;
using Game.Scene;
using Game.Data;
using Game.Gameplay.Fleet;

namespace Game.UI {
    /// <summary>
    /// StarMap UI — strategic layer rendering and interaction.
    ///
    /// Subscribes to (ADR-0002 Tier 2 C# events):
    /// - StarMapData nodes/edges — Painter2D rendering
    /// - FleetDispatchSystem — ship icon lifecycle (OnDispatchCreated/OnOrderClosed)
    /// - OnResourcesUpdatedChannel — resource corner display
    /// - ViewLayerChannel — show/hide on STARMAP layer
    ///
    /// Implements TR-starmapui-001~TR-starmapui-004.
    /// Architecture: ADR-0020.
    /// </summary>
    public class StarMapUI : MonoBehaviour
    {
        // ─── Serialized Fields ──────────────────────────────────────────
        [Header("UI Toolkit")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("Channels")]
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private ShipStateChannel _shipStateChannel;
        [SerializeField] private OnResourcesUpdatedChannel _resourcesChannel;

        [Header("Data")]
        [SerializeField] private Camera _starmapCamera;

        // ─── Painter2D Constants (dp units — from GDD) ─────────────────
        private const float NODE_HOME_SIZE   = 56f;
        private const float NODE_STANDARD_SIZE = 44f;
        private const float NODE_RICH_SIZE   = 48f;
        private const float HOTSPOT_HOME     = 64f;
        private const float HOTSPOT_STANDARD = 56f;
        private const float HOTSPOT_RICH     = 60f;

        // Colors (from GDD color matrix)
        private static readonly Color COLOR_PLAYER      = new Color(0.133f, 0.4f, 0.8f);          // #2266CC
        private static readonly Color COLOR_ENEMY       = new Color(1.0f, 0.267f, 0.0f);        // #FF4400
        private static readonly Color COLOR_NEUTRAL     = new Color(0.533f, 0.533f, 0.533f);     // #888888
        private static readonly Color COLOR_EDGE        = new Color(0.267f, 0.267f, 0.4f);        // #444466
        private static readonly Color COLOR_FLEET_PATH  = new Color(0.267f, 0.533f, 1.0f);       // #4488FF

        // Zoom bounds
        private const float ZOOM_MIN = 0.5f;
        private const float ZOOM_MAX = 2.0f;

        // ─── State ─────────────────────────────────────────────────────
        private VisualElement _viewport;
        private VisualElement _fleetIconRoot;
        private VisualElement _resourceCorner;
        private Label _oreLabel;
        private Label _energyLabel;
        private Label _simRateLabel;

        private StarMapData _mapData;
        private Dictionary<string, VisualElement> _fleetIcons = new Dictionary<string, VisualElement>();

        // Interaction state
        private enum InteractionState { IDLE, NODE_SELECTED, SHIP_SELECTED, DISPATCH_CONFIRM }
        private InteractionState _state = InteractionState.IDLE;
        private string _selectedNodeId;
        private string _selectedShipId;
        private string _dispatchTargetNodeId;

        // Zoom / pan
        private float _zoomScale = 1f;
        private Vector2 _panOffset = Vector2.zero;
        private Vector2 _lastTouchPos;
        private bool _isPanning;

        // ─── Unity Lifecycle ───────────────────────────────────────────

        private void OnEnable()
        {
            _viewLayerChannel.Subscribe(OnViewLayerChanged);
            _shipStateChannel.Subscribe(OnShipStateChanged);

            if (FleetDispatchSystem.Instance != null) {
                FleetDispatchSystem.Instance.OnDispatchCreated += OnDispatchCreated;
                FleetDispatchSystem.Instance.OnOrderClosed    += OnOrderClosed;
            }
            if (_resourcesChannel != null) {
                _resourcesChannel.Subscribe(OnResourcesUpdated);
            }

            _mapData = GameDataManager.Instance?.GetStarMapData();
            BuildUI();
        }

        private void OnDisable()
        {
            _viewLayerChannel.Unsubscribe(OnViewLayerChanged);
            _shipStateChannel.Unsubscribe(OnShipStateChanged);

            if (FleetDispatchSystem.Instance != null) {
                FleetDispatchSystem.Instance.OnDispatchCreated -= OnDispatchCreated;
                FleetDispatchSystem.Instance.OnOrderClosed    -= OnOrderClosed;
            }
            if (_resourcesChannel != null) {
                _resourcesChannel.Unsubscribe(OnResourcesUpdated);
            }
        }

        private void Update()
        {
            UpdateFleetIconPositions();
        }

        // ─── UI Build ──────────────────────────────────────────────────

        private void BuildUI()
        {
            if (_uiDocument == null) return;

            var root = _uiDocument.rootVisualElement;
            _viewport        = root.Q<VisualElement>("starmap-viewport");
            _fleetIconRoot  = root.Q<VisualElement>("fleet-icon-root");
            _resourceCorner = root.Q<VisualElement>("resource-corner");
            _oreLabel       = root.Q<Label>("ore-label");
            _energyLabel    = root.Q<Label>("energy-label");
            _simRateLabel   = root.Q<Label>("simrate-label");

            // Painter2D rendering on viewport
            if (_viewport != null) {
                _viewport.generateVisualContent += OnGenerateVisualContent;
                _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
                _viewport.RegisterCallback<WheelEvent>(OnWheel);
                _viewport.pickingMode = PickingMode.Position;
            }
        }

        // ─── Painter2D Rendering ─────────────────────────────────────────

        private void OnGenerateVisualContent(Painter2D painter)
        {
            if (_mapData == null) return;

            RenderEdges(painter);
            RenderNodes(painter);
            RenderPathPreview(painter);
        }

        private void RenderNodes(Painter2D painter)
        {
            foreach (var node in _mapData.Nodes) {
                if (node.FogState == FogState.UNEXPLORED) continue;

                float size  = GetNodeSize(node.NodeType);
                Vector2 pos = WorldToScreen(node.Position);
                Color color = GetNodeColor(node);

                if (node.FogState == FogState.EXPLORED) {
                    color.a = 0.5f;
                }

                switch (node.NodeType) {
                    case NodeType.HOME_BASE:
                        DrawHexagon(painter, pos, size, color);
                        break;
                    case NodeType.RICH:
                        DrawDiamond(painter, pos, size, color);
                        break;
                    default:
                        DrawCircle(painter, pos, size, color);
                        break;
                }
            }
        }

        private void RenderEdges(Painter2D painter)
        {
            foreach (var edge in _mapData.Edges) {
                var from = _mapData.GetNode(edge.FromNodeId);
                var to   = _mapData.GetNode(edge.ToNodeId);
                if (from == null || to == null) continue;

                // Hide edges where both ends are UNEXPLORED
                if (from.FogState == FogState.UNEXPLORED && to.FogState == FogState.UNEXPLORED) continue;

                bool hasFleetInTransit = false; // TODO: query FleetDispatchSystem
                Color edgeColor = hasFleetInTransit ? COLOR_FLEET_PATH : COLOR_EDGE;
                float width    = hasFleetInTransit ? 3f : 1.5f;

                painter.strokeColor = edgeColor;
                painter.lineWidth   = width;
                painter.BeginPath();
                painter.MoveTo(WorldToScreen(from.Position));
                painter.LineTo(WorldToScreen(to.Position));
                painter.Stroke();
            }
        }

        private void RenderPathPreview(Painter2D painter)
        {
            if (_state != InteractionState.DISPATCH_CONFIRM) return;
            if (string.IsNullOrEmpty(_selectedShipId) || string.IsNullOrEmpty(_dispatchTargetNodeId)) return;

            var path = StarMapPathfinder.FindPath(_mapData, _selectedShipId, _dispatchTargetNodeId);
            if (path == null || path.Count < 2) return;

            painter.strokeColor = COLOR_FLEET_PATH;
            painter.lineWidth   = 3f;
            painter.BeginPath();

            bool first = true;
            foreach (var nodeId in path) {
                var node = _mapData.GetNode(nodeId);
                if (node == null) continue;
                if (first) { painter.MoveTo(WorldToScreen(node.Position)); first = false; }
                else        { painter.LineTo(WorldToScreen(node.Position)); }
            }
            painter.Stroke();
        }

        // ─── Shape Drawing ─────────────────────────────────────────────

        private void DrawCircle(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.arc(center, radius, 0f, 360f);
            painter.Fill();
        }

        private void DrawHexagon(Painter2D painter, Vector2 center, float size, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            for (int i = 0; i < 6; i++) {
                float angle = Mathf.Deg2Rad * (60f * i - 30f);
                Vector2 pt = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size;
                if (i == 0) painter.MoveTo(pt);
                else        painter.LineTo(pt);
            }
            painter.ClosePath();
            painter.Fill();
        }

        private void DrawDiamond(Painter2D painter, Vector2 center, float size, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(center + new Vector2(0f,   size));
            painter.LineTo(center + new Vector2(size,  0f));
            painter.LineTo(center + new Vector2(0f,  -size));
            painter.LineTo(center + new Vector2(-size, 0f));
            painter.ClosePath();
            painter.Fill();
        }

        // ─── Coordinate Conversion ─────────────────────────────────────

        private Vector2 WorldToScreen(Vector2 dpPosition)
            => dpPosition * _zoomScale + _panOffset;

        private Vector2 ScreenToWorld(Vector2 screenPosition)
            => (screenPosition - _panOffset) / _zoomScale;

        // ─── Node Helpers ─────────────────────────────────────────────

        private float GetNodeSize(NodeType type) => type switch {
            NodeType.HOME_BASE => NODE_HOME_SIZE,
            NodeType.RICH      => NODE_RICH_SIZE,
            _                  => NODE_STANDARD_SIZE,
        };

        private float GetHotspotSize(NodeType type) => type switch {
            NodeType.HOME_BASE => HOTSPOT_HOME,
            NodeType.RICH      => HOTSPOT_RICH,
            _                  => HOTSPOT_STANDARD,
        };

        private Color GetNodeColor(StarNode node) => node.Ownership switch {
            OwnershipState.PLAYER => COLOR_PLAYER,
            OwnershipState.ENEMY  => COLOR_ENEMY,
            _                     => COLOR_NEUTRAL,
        };

        // ─── Touch / Mouse Input ───────────────────────────────────────

        private void OnPointerDown(PointerDownEvent evt)
        {
            _lastTouchPos = evt.position;
            _isPanning = false;

            Vector2 worldPos = ScreenToWorld(evt.position);
            string hitId = HitTestNode(worldPos);

            if (!string.IsNullOrEmpty(hitId)) {
                HandleNodeTap(hitId);
            } else {
                HandleBackgroundTap();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pressedButtons == 1) {
                Vector2 delta = evt.position - _lastTouchPos;
                if (delta.magnitude > 2f) _isPanning = true;
                if (_isPanning) {
                    _panOffset += delta;
                    _lastTouchPos = evt.position;
                    _viewport?.MarkDirtyContent();
                }
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _isPanning = false;
        }

        private void OnWheel(WheelEvent evt)
        {
            float factor = evt.delta.y > 0 ? 1.1f : 0.9f;
            _zoomScale = Mathf.Clamp(_zoomScale * factor, ZOOM_MIN, ZOOM_MAX);
            _viewport?.MarkDirtyContent();
        }

        private string HitTestNode(Vector2 worldPos)
        {
            if (_mapData == null) return null;
            foreach (var node in _mapData.Nodes) {
                if (node.FogState == FogState.UNEXPLORED) continue;
                float hotspot = GetHotspotSize(node.NodeType);
                if (Vector2.Distance(worldPos, node.Position) <= hotspot) {
                    return node.Id;
                }
            }
            return null;
        }

        // ─── Interaction State Machine ─────────────────────────────────

        private void HandleNodeTap(string nodeId)
        {
            switch (_state) {
                case InteractionState.IDLE:
                    _selectedNodeId = nodeId;
                    _state = InteractionState.NODE_SELECTED;
                    break;

                case InteractionState.NODE_SELECTED:
                    if (nodeId == _selectedNodeId) {
                        _state = InteractionState.SHIP_SELECTED;
                    } else {
                        _selectedNodeId = nodeId;
                    }
                    break;

                case InteractionState.SHIP_SELECTED:
                    if (IsValidDispatchTarget(nodeId)) {
                        _dispatchTargetNodeId = nodeId;
                        _state = InteractionState.DISPATCH_CONFIRM;
                        ShowDispatchConfirm(_selectedShipId, nodeId);
                    }
                    break;

                case InteractionState.DISPATCH_CONFIRM:
                    _state = InteractionState.IDLE;
                    ClearSelection();
                    break;
            }
        }

        private void HandleBackgroundTap()
        {
            _state = InteractionState.IDLE;
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedNodeId     = null;
            _selectedShipId     = null;
            _dispatchTargetNodeId = null;
            _viewport?.MarkDirtyContent();
        }

        private bool IsValidDispatchTarget(string nodeId)
        {
            if (_mapData == null || string.IsNullOrEmpty(_selectedShipId)) return false;
            if (nodeId == _selectedShipId) return false;
            var path = StarMapPathfinder.FindPath(_mapData, _selectedShipId, nodeId);
            return path != null && path.Count > 0;
        }

        // ─── Dispatch Confirmation ────────────────────────────────────

        private void ShowDispatchConfirm(string shipId, string targetNodeId)
        {
            // TODO: Show confirmation card UI (cost, ETA).
            // On confirm → FleetDispatchSystem.Instance.RequestDispatch(...)
            // On cancel  → _state = IDLE, clear selection
        }

        // ─── Fleet Icon Management ────────────────────────────────────

        private void OnDispatchCreated(DispatchOrder order)
        {
            if (_fleetIconRoot == null) return;
            var originNode = _mapData?.GetNode(order.OriginNodeId);
            if (originNode == null) return;

            // Create icon at origin node position
            var icon = new VisualElement();
            icon.style.width = 24;
            icon.style.height = 24;
            icon.style.backgroundColor = COLOR_FLEET_PATH;
            icon.style.borderTopLeftRadius = 12;
            icon.style.borderTopRightRadius = 12;
            icon.style.borderBottomLeftRadius = 12;
            icon.style.borderBottomRightRadius = 12;

            Vector2 screenPos = WorldToScreen(originNode.Position);
            icon.style.left = screenPos.x - 12;
            icon.style.top  = screenPos.y - 12;

            _fleetIconRoot.Add(icon);
            _fleetIcons[order.OrderId] = icon;
        }

        private void OnOrderClosed(string orderId)
        {
            if (_fleetIcons.TryGetValue(orderId, out var icon)) {
                icon.RemoveFromHierarchy();
                _fleetIcons.Remove(orderId);
            }
        }

        private void UpdateFleetIconPositions()
        {
            if (FleetDispatchSystem.Instance == null) return;

            foreach (var kvp in _fleetIcons) {
                var order = GetActiveOrder(kvp.Key);
                if (order == null) continue;

                float progress = order.HopProgress / 3f; // 3s per hop
                var (x, y) = InterpolateAlongPath(order, progress);
                Vector2 screenPos = WorldToScreen(new Vector2(x, y));
                kvp.Value.style.left = screenPos.x - 12;
                kvp.Value.style.top  = screenPos.y - 12;
            }
        }

        private DispatchOrder GetActiveOrder(string orderId)
        {
            // FleetDispatchSystem should expose a method to get order by ID
            // For now, skip position updates until the API is confirmed
            return null;
        }

        private (float x, float y) InterpolateAlongPath(DispatchOrder order, float progress)
        {
            // TODO: Interpolate position along the locked path based on hop progress
            return (0f, 0f);
        }

        // ─── Resource Display ───────────────────────────────────────────

        private void OnResourcesUpdated(ResourceSnapshot snapshot)
        {
            if (_oreLabel    != null) _oreLabel.text    = $"{snapshot.Ore}";
            if (_energyLabel != null) _energyLabel.text = $"{snapshot.Energy}";
        }

        // ─── View Layer ────────────────────────────────────────────────

        private void OnViewLayerChanged(ViewLayer newLayer)
        {
            bool visible = newLayer == ViewLayer.STARMAP;
            if (_uiDocument != null) {
                _uiDocument.rootVisualElement.style.display = visible
                    ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>Zooms to fit all visible nodes within the viewport.</summary>
        public void FitToContent()
        {
            if (_mapData == null || _viewport == null) return;
            // Compute bounding box of all non-UNEXPLORED nodes,
            // derive zoom and pan to center the content.
        }

        /// <summary>Current interaction state (for debugging).</summary>
        public InteractionState CurrentState => _state;
    }
}
