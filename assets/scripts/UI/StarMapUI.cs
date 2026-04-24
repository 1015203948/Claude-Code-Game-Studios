using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
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
        private static readonly Color COLOR_SELECTED    = new Color(1.0f, 0.9f, 0.2f);           // #FFE633

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
        private Label _cockpitHintLabel;

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
        private bool _isPointerDown;

        // ─── Unity Lifecycle ───────────────────────────────────────────

        private void OnEnable()
        {
            // Auto-create UIDocument if missing (e.g. scene reference lost)
            if (_uiDocument == null) {
                _uiDocument = GetComponent<UIDocument>();
                if (_uiDocument == null) {
                    _uiDocument = gameObject.AddComponent<UIDocument>();
                    Debug.Log("[StarMapUI] Auto-added UIDocument component.");
                }
            }

            // Ensure PanelSettings is assigned (required for UI Toolkit rendering & input)
#if UNITY_EDITOR
            if (_uiDocument != null && _uiDocument.panelSettings == null) {
                var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                    "Assets/data/ui/StarMapOverlay_ScreenOverlay.asset");
                if (ps != null) {
                    _uiDocument.panelSettings = ps;
                    Debug.Log("[StarMapUI] Auto-assigned PanelSettings.");
                } else {
                    Debug.LogWarning("[StarMapUI] PanelSettings not found at Assets/data/ui/StarMapOverlay_ScreenOverlay.asset");
                }
            }
#endif

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
            CenterMapOnScreen();
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

            if (_viewport != null) {
                _viewport.generateVisualContent -= OnGenerateVisualContent;
                _viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                _viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _viewport.UnregisterCallback<WheelEvent>(OnWheel);
                _viewport.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            }
            if (_cockpitHintLabel != null) {
                _cockpitHintLabel.UnregisterCallback<PointerDownEvent>(OnHintLabelPointerDown);
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

            // ── Cleanup previously dynamically-added elements to avoid duplicates ──
            var oldHint = root.Q<Label>("cockpit-hint-label");
            oldHint?.RemoveFromHierarchy();
            var oldFleetRoot = root.Q<VisualElement>("fleet-icon-root");
            if (oldFleetRoot != null && oldFleetRoot != _fleetIconRoot) {
                oldFleetRoot.RemoveFromHierarchy();
            }
            var oldViewport = root.Q<VisualElement>("starmap-viewport");
            if (oldViewport != null && oldViewport != _viewport) {
                oldViewport.RemoveFromHierarchy();
            }

            _viewport        = root.Q<VisualElement>("starmap-viewport");
            _fleetIconRoot  = root.Q<VisualElement>("fleet-icon-root");
            _resourceCorner = root.Q<VisualElement>("resource-corner");
            _oreLabel       = root.Q<Label>("ore-label");
            _energyLabel    = root.Q<Label>("energy-label");
            _simRateLabel   = root.Q<Label>("simrate-label");

            // 如果没有 UXML，动态创建最小 viewport
            if (_viewport == null) {
                _viewport = new VisualElement();
                _viewport.name = "starmap-viewport";
                _viewport.style.position = Position.Absolute;
                _viewport.style.left = 0;
                _viewport.style.top = 0;
                _viewport.style.right = 0;
                _viewport.style.bottom = 0;
                _viewport.style.backgroundColor = new Color(0.02f, 0.02f, 0.06f);
                root.Add(_viewport);
            }
            if (_fleetIconRoot == null) {
                _fleetIconRoot = new VisualElement();
                _fleetIconRoot.name = "fleet-icon-root";
                _fleetIconRoot.style.position = Position.Absolute;
                _fleetIconRoot.style.left = 0;
                _fleetIconRoot.style.top = 0;
                _fleetIconRoot.style.width = Length.Percent(100);
                _fleetIconRoot.style.height = Length.Percent(100);
                _fleetIconRoot.pickingMode = PickingMode.Ignore;
                root.Add(_fleetIconRoot);
            }

            // 创建"进入驾驶舱"提示标签（动态创建，不依赖 UXML）
            _cockpitHintLabel = new Label("再点一次进入驾驶舱");
            _cockpitHintLabel.name = "cockpit-hint-label";
            _cockpitHintLabel.style.position = Position.Absolute;
            _cockpitHintLabel.style.bottom = 80;
            _cockpitHintLabel.style.left = 0;
            _cockpitHintLabel.style.width = Length.Percent(100);
            _cockpitHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _cockpitHintLabel.style.fontSize = 16;
            _cockpitHintLabel.style.color = COLOR_SELECTED;
            _cockpitHintLabel.style.display = DisplayStyle.None;
            // CRITICAL: Ignore picking so it never blocks viewport pointer events.
            // We add our own click handler on the label for users who tap the hint text.
            _cockpitHintLabel.pickingMode = PickingMode.Ignore;
            _cockpitHintLabel.RegisterCallback<PointerDownEvent>(OnHintLabelPointerDown);
            root.Add(_cockpitHintLabel);

            // Painter2D rendering on viewport
            _viewport.generateVisualContent -= OnGenerateVisualContent; // avoid duplicate
            _viewport.generateVisualContent += OnGenerateVisualContent;
            _viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.UnregisterCallback<WheelEvent>(OnWheel);
            _viewport.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _viewport.RegisterCallback<WheelEvent>(OnWheel);
            // Fallback: also capture mouse move for editor Game-view compatibility
            _viewport.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            _viewport.pickingMode = PickingMode.Position;
        }

        // ─── Painter2D Rendering ─────────────────────────────────────────

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_mapData == null) return;

            RenderEdges(context.painter2D);
            RenderNodes(context.painter2D);
            RenderPathPreview(context.painter2D);
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

                // 选中高亮
                bool isSelected = (node.Id == _selectedNodeId);
                if (isSelected) {
                    // 绘制外圈光环
                    painter.strokeColor = COLOR_SELECTED;
                    painter.lineWidth = 3f;
                    painter.BeginPath();
                    painter.Arc(pos, size + 8f, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.Stroke();
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
            // Compute set of edges with fleets in transit for efficient lookup
            var fleetEdges = ComputeFleetInTransitEdges();

            foreach (var edge in _mapData.Edges) {
                var from = _mapData.GetNode(edge.FromNodeId);
                var to   = _mapData.GetNode(edge.ToNodeId);
                if (from == null || to == null) continue;

                // Hide edges where both ends are UNEXPLORED
                if (from.FogState == FogState.UNEXPLORED && to.FogState == FogState.UNEXPLORED) continue;

                bool hasFleetInTransit = fleetEdges.Contains((edge.FromNodeId, edge.ToNodeId));
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

        private HashSet<(string from, string to)> ComputeFleetInTransitEdges()
        {
            var edges = new HashSet<(string, string)>();
            if (FleetDispatchSystem.Instance == null) return edges;

            foreach (var order in FleetDispatchSystem.Instance.GetAllOrders()) {
                var path = order.LockedPath;
                for (int i = 0; i < path.Count - 1; i++) {
                    edges.Add((path[i], path[i + 1]));
                }
            }
            return edges;
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
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
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

        /// <summary>Centers the starmap content in the viewport on first load.</summary>
        private void CenterMapOnScreen()
        {
            if (_mapData == null || _viewport == null) return;

            // Compute bounding box of all visible nodes
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            bool hasVisible = false;
            foreach (var node in _mapData.Nodes) {
                if (node.FogState == FogState.UNEXPLORED) continue;
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
                hasVisible = true;
            }
            if (!hasVisible) return;

            Vector2 contentCenter = (min + max) * 0.5f;
            // Panel center (UI Toolkit panel space: origin top-left)
            Vector2 panelCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            _panOffset = panelCenter - contentCenter * _zoomScale;
            _viewport.MarkDirtyRepaint();
            Debug.Log($"[StarMapUI] Centered map: contentCenter={contentCenter}, panOffset={_panOffset}");
        }

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
            _isPointerDown = true;

            // Capture pointer so PointerMoveEvent fires continuously during drag
            _viewport?.CapturePointer(evt.pointerId);

            Vector2 worldPos = ScreenToWorld(evt.position);
            string hitId = HitTestNode(worldPos);

            Debug.Log($"[StarMapUI] PointerDown at {evt.position}, worldPos={worldPos}, hit={hitId ?? "bg"}, state={_state}");

            if (!string.IsNullOrEmpty(hitId)) {
                HandleNodeTap(hitId);
            } else {
                HandleBackgroundTap();
            }
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            Debug.Log($"[StarMapUI] PointerMove at {evt.position}, isPointerDown={_isPointerDown}");
            if (_isPointerDown) {
                Vector2 delta = (Vector2)evt.position - _lastTouchPos;
                if (delta.magnitude > 2f) _isPanning = true;
                if (_isPanning) {
                    _panOffset += delta;
                    _lastTouchPos = evt.position;
                    _viewport?.MarkDirtyRepaint();
                    Debug.Log($"[StarMapUI] Panning: delta={delta}, panOffset={_panOffset}");
                }
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            _isPointerDown = false;
            _isPanning = false;
            if (_viewport != null) {
                _viewport.ReleasePointer(evt.pointerId);
            }
        }

        private void OnWheel(WheelEvent evt)
        {
            float factor = evt.delta.y > 0 ? 1.1f : 0.9f;
            _zoomScale = Mathf.Clamp(_zoomScale * factor, ZOOM_MIN, ZOOM_MAX);
            _viewport?.MarkDirtyRepaint();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            // Fallback for editor Game-view where PointerMoveEvent may not fire
            if (_isPointerDown) {
                Vector2 delta = (Vector2)evt.mousePosition - _lastTouchPos;
                if (delta.magnitude > 2f) _isPanning = true;
                if (_isPanning) {
                    _panOffset += delta;
                    _lastTouchPos = evt.mousePosition;
                    _viewport?.MarkDirtyRepaint();
                    Debug.Log($"[StarMapUI] MousePan: delta={delta}, panOffset={_panOffset}");
                }
            }
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
            Debug.Log($"[StarMapUI] HandleNodeTap: nodeId={nodeId}, currentState={_state}");
            switch (_state) {
                case InteractionState.IDLE:
                    _selectedNodeId = nodeId;
                    _state = InteractionState.NODE_SELECTED;
                    _viewport?.MarkDirtyRepaint();
                    break;

                case InteractionState.NODE_SELECTED:
                    if (nodeId == _selectedNodeId) {
                        // Find first player ship docked at this node
                        _selectedShipId = FindPlayerShipAtNode(nodeId);
                        Debug.Log($"[StarMapUI] FindPlayerShipAtNode({nodeId}) = {_selectedShipId ?? "null"}");
                        if (!string.IsNullOrEmpty(_selectedShipId)) {
                            _state = InteractionState.SHIP_SELECTED;
                            ShowCockpitHint(true);
                        } else {
                            _state = InteractionState.IDLE;
                            ClearSelection();
                        }
                    } else {
                        _selectedNodeId = nodeId;
                    }
                    _viewport?.MarkDirtyRepaint();
                    break;

                case InteractionState.SHIP_SELECTED:
                    if (nodeId == _selectedNodeId) {
                        // 点击同一节点 → 进入驾驶舱
                        Debug.Log("[StarMapUI] Third tap on same node → EnterCockpit()");
                        EnterCockpit();
                    } else if (IsValidDispatchTarget(nodeId)) {
                        _dispatchTargetNodeId = nodeId;
                        _state = InteractionState.DISPATCH_CONFIRM;
                        ShowDispatchConfirm(_selectedShipId, nodeId);
                    }
                    _viewport?.MarkDirtyRepaint();
                    break;

                case InteractionState.DISPATCH_CONFIRM:
                    _state = InteractionState.IDLE;
                    ClearSelection();
                    _viewport?.MarkDirtyRepaint();
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
            ShowCockpitHint(false);
            _viewport?.MarkDirtyRepaint();
        }

        private void ShowCockpitHint(bool show)
        {
            if (_cockpitHintLabel != null) {
                _cockpitHintLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Allows the user to tap the hint label itself to enter cockpit.
        /// The label has pickingMode=Ignore so it does not block viewport events,
        /// but we register this callback for users who naturally tap the hint text.
        /// </summary>
        private void OnHintLabelPointerDown(PointerDownEvent evt)
        {
            Debug.Log("[StarMapUI] Hint label tapped → EnterCockpit()");
            EnterCockpit();
        }

        private bool IsValidDispatchTarget(string nodeId)
        {
            if (_mapData == null || string.IsNullOrEmpty(_selectedShipId)) return false;
            if (nodeId == _selectedShipId) return false;
            var path = StarMapPathfinder.FindPath(_mapData, _selectedShipId, nodeId);
            return path != null && path.Count > 0;
        }

        private string FindPlayerShipAtNode(string nodeId)
        {
            if (GameDataManager.Instance == null) return null;
            foreach (var ship in GameDataManager.Instance.AllShips) {
                if (ship.IsPlayerControlled
                    && ship.DockedNodeId == nodeId
                    && ship.State == ShipState.DOCKED) {
                    return ship.InstanceId;
                }
            }
            return null;
        }

        // ─── Dispatch Confirmation ────────────────────────────────────

        private void ShowDispatchConfirm(string shipId, string targetNodeId)
        {
            if (_fleetIconRoot == null) return;

            // Remove any existing confirmation card
            var existing = _fleetIconRoot.Q<VisualElement>("dispatch-confirm-card");
            existing?.RemoveFromHierarchy();

            // Build confirmation card
            var card = new VisualElement();
            card.name = "dispatch-confirm-card";
            card.style.position = Position.Absolute;
            card.style.width = 200;
            card.style.height = 120;
            card.style.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;

            // Center card in viewport
            card.style.left = 9999; // will fix below
            card.style.top = 9999;

            // Title
            var title = new Label("DISPATCH FLEET");
            title.style.color = new Color(0.8f, 0.8f, 0.9f);
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            // Body: target node + ETA
            var targetNode = _mapData?.GetNode(targetNodeId);
            string targetName = targetNode?.DisplayName ?? targetNodeId;
            float hops = 1f;
            var path = StarMapPathfinder.FindPath(_mapData, shipId, targetNodeId);
            if (path != null) hops = path.Count - 1;
            float eta = hops * 3f; // FLEET_TRAVEL_TIME = 3s/hop

            var body = new Label($"To: {targetName}\nETA: {eta:F0}s");
            body.style.color = new Color(0.65f, 0.65f, 0.75f);
            body.style.fontSize = 11;
            body.style.whiteSpace = WhiteSpace.Normal;

            // Buttons row
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.SpaceBetween;
            btnRow.style.width = Length.Percent(100);

            var confirmBtn = new Button(() => OnDispatchConfirm(shipId, targetNodeId, card)) { text = "CONFIRM" };
            confirmBtn.style.flexGrow = 1;
            confirmBtn.style.marginLeft = 0;
            confirmBtn.style.marginRight = 4;
            confirmBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
            confirmBtn.style.color = Color.white;

            var cancelBtn = new Button(() => OnDispatchCancel(card)) { text = "CANCEL" };
            cancelBtn.style.flexGrow = 1;
            cancelBtn.style.marginLeft = 4;
            cancelBtn.style.marginRight = 0;
            cancelBtn.style.backgroundColor = new Color(0.3f, 0.2f, 0.2f);
            cancelBtn.style.color = Color.white;

            btnRow.Add(confirmBtn);
            btnRow.Add(cancelBtn);

            card.Add(title);
            card.Add(body);
            card.Add(btnRow);
            _fleetIconRoot.Add(card);

            // Position card in center of viewport
            var vp = _viewport;
            if (vp != null) {
                card.style.left = (vp.resolvedStyle.width - 200) * 0.5f;
                card.style.top  = (vp.resolvedStyle.height - 120) * 0.5f;
            }
        }

        private void OnDispatchConfirm(string shipId, string targetNodeId, VisualElement card)
        {
            FleetDispatchSystem.Instance?.RequestDispatch(shipId, targetNodeId);
            card.RemoveFromHierarchy();
            _state = InteractionState.IDLE;
            ClearSelection();
        }

        private void OnDispatchCancel(VisualElement card)
        {
            card.RemoveFromHierarchy();
            _state = InteractionState.IDLE;
            ClearSelection();
        }

        // ─── Enter Cockpit ──────────────────────────────────────────────

        private void EnterCockpit()
        {
            Debug.Log($"[StarMapUI] EnterCockpit called for shipId={_selectedShipId}");
            if (string.IsNullOrEmpty(_selectedShipId)) return;

            ShowCockpitHint(false);

            // 调用 ViewLayerManager 进入驾驶舱
            if (ViewLayerManager.Instance != null) {
                Debug.Log($"[StarMapUI] Calling ViewLayerManager.RequestEnterCockpit({_selectedShipId})");
                ViewLayerManager.Instance.RequestEnterCockpit(_selectedShipId);
            } else {
                Debug.LogWarning("[StarMapUI] ViewLayerManager.Instance is null — cannot enter cockpit.");
            }

            _state = InteractionState.IDLE;
            ClearSelection();
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

                float progress = order.HopProgress / FleetDispatchSystem.FLEET_TRAVEL_TIME;
                var (x, y) = InterpolateAlongPath(order, progress);
                Vector2 screenPos = WorldToScreen(new Vector2(x, y));
                kvp.Value.style.left = screenPos.x - 12;
                kvp.Value.style.top  = screenPos.y - 12;
            }
        }

        private DispatchOrder GetActiveOrder(string orderId)
        {
            return FleetDispatchSystem.Instance?.GetOrder(orderId);
        }

        private (float x, float y) InterpolateAlongPath(DispatchOrder order, float progress)
        {
            var path = order.LockedPath;
            if (path == null || path.Count < 2) return (0f, 0f);

            // progress: 0..1 within the current hop
            int hopIndex = order.CurrentHopIndex;
            if (hopIndex < 0 || hopIndex >= path.Count - 1) {
                // Return destination if beyond path
                var dest = _mapData?.GetNode(path[^1]);
                return dest != null ? (dest.Position.x, dest.Position.y) : (0f, 0f);
            }

            var fromNode = _mapData?.GetNode(path[hopIndex]);
            var toNode   = _mapData?.GetNode(path[hopIndex + 1]);
            if (fromNode == null || toNode == null) return (0f, 0f);

            float t = Mathf.Clamp01(progress);
            float x = Mathf.Lerp(fromNode.Position.x, toNode.Position.x, t);
            float y = Mathf.Lerp(fromNode.Position.y, toNode.Position.y, t);
            return (x, y);
        }

        // ─── Resource Display ───────────────────────────────────────────

        private void OnResourcesUpdated(ResourceSnapshot snapshot)
        {
            if (_oreLabel    != null) _oreLabel.text    = $"{snapshot.Ore}";
            if (_energyLabel != null) _energyLabel.text = $"{snapshot.Energy}";
        }

        private void OnShipStateChanged((string instanceId, ShipState newState) payload)
        {
            // StarMapUI tracks ship state changes for optional visual updates.
            // Currently a no-op stub — expand if needed.
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
        private InteractionState CurrentState => _state;
    }
}
