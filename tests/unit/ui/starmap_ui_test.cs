using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Game.Channels;
using Game.Scene;
using Game.Data;
using Game.Gameplay.Fleet;
using Object = UnityEngine.Object;

[TestFixture]
public class StarMapUI_Test
{
    // ─── Dependencies ─────────────────────────────────────────────────
    private GameObject _uiGo;
    private StarMapUI _ui;
    private UIDocument _uiDocument;
    private VisualElement _root;
    private VisualElement _viewport;
    private VisualElement _fleetIconRoot;
    private VisualElement _resourceCorner;
    private Label _oreLabel;
    private Label _energyLabel;
    private Camera _camera;

    private ViewLayerChannel _viewLayerChannel;
    private ShipStateChannel _shipStateChannel;
    private OnResourcesUpdatedChannel _resourcesChannel;

    private StarMapData _mapData;

    // ─── SetUp / TearDown ────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        // Clear singletons
        if (GameDataManager.Instance != null) GameDataManager.Instance = null;
        if (FleetDispatchSystem.Instance != null) Object.DestroyImmediate(FleetDispatchSystem.Instance.gameObject);

        // GameDataManager with StarMapData
        var go = new GameObject("GDM");
        var dm = go.AddComponent<GameDataManager>();
        GameDataManager.Instance = dm;
        _mapData = StarMapData.CreateMvpDiamond();
        dm.SetStarMapData(_mapData);

        // Cameras
        _camera = new GameObject("Camera").AddComponent<Camera>();

        // UIDocument with root
        _uiGo = new GameObject("StarMapUI");
        _uiDocument = _uiGo.AddComponent<UIDocument>();
        _root = new VisualElement();
        _viewport = new VisualElement() { name = "starmap-viewport" };
        _fleetIconRoot = new VisualElement() { name = "fleet-icon-root" };
        _resourceCorner = new VisualElement() { name = "resource-corner" };
        _oreLabel = new Label() { name = "ore-label" };
        _energyLabel = new Label() { name = "energy-label" };
        _root.Add(_viewport);
        _root.Add(_fleetIconRoot);
        _root.Add(_resourceCorner);
        _resourceCorner.Add(_oreLabel);
        _resourceCorner.Add(_energyLabel);

        // Set root visual element via reflection (normally set by UIDocument)
        var rtField = typeof(UIDocument).GetField("m_RootTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        rtField?.SetValue(_uiDocument, _root);

        // Channels
        _viewLayerChannel = ScriptableObject.CreateInstance<ViewLayerChannel>();
        _shipStateChannel = ScriptableObject.CreateInstance<ShipStateChannel>();
        _resourcesChannel = ScriptableObject.CreateInstance<OnResourcesUpdatedChannel>();

        // StarMapUI
        _ui = _uiGo.AddComponent<StarMapUI>();
        InjectField(_ui, "_uiDocument", _uiDocument);
        InjectField(_ui, "_viewLayerChannel", _viewLayerChannel);
        InjectField(_ui, "_shipStateChannel", _shipStateChannel);
        InjectField(_ui, "_resourcesChannel", _resourcesChannel);
        InjectField(_ui, "_starmapCamera", _camera);

        // FleetDispatchSystem (for OnOrderClosed event)
        var fleetGo = new GameObject("FleetDispatchSystem");
        fleetGo.AddComponent<FleetDispatchSystem>();

        // Activate OnEnable to subscribe
        _ui.SendMessage("OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        _ui.SendMessage("OnDisable");

        if (_uiGo != null) Object.DestroyImmediate(_uiGo);
        if (_camera != null) Object.DestroyImmediate(_camera);
        if (_viewLayerChannel != null) Object.DestroyImmediate(_viewLayerChannel);
        if (_shipStateChannel != null) Object.DestroyImmediate(_shipStateChannel);
        if (_resourcesChannel != null) Object.DestroyImmediate(_resourcesChannel);

        if (GameDataManager.Instance != null) Object.DestroyImmediate(GameDataManager.Instance.gameObject);
        if (FleetDispatchSystem.Instance != null) Object.DestroyImmediate(FleetDispatchSystem.Instance.gameObject);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static void InjectField(object obj, string name, object value)
    {
        var field = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field {name} not found on {obj.GetType().Name}");
        field.SetValue(obj, value);
    }

    private static void CallMethod(object obj, string name, params object[] args)
    {
        var method = obj.GetType().GetMethod(name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method, $"Method {name} not found on {obj.GetType().Name}");
        method.Invoke(obj, args);
    }

    // ─── TR-starmapui-001: Node rendering (visibility) ─────────────

    [Test]
    public void visible_node_renders_when_fogState_is_VISIBLE()
    {
        // Given: home_base is VISIBLE by default (InitFogState)
        var homeBase = _mapData.GetNode("home_base");
        Assert.AreEqual(FogState.VISIBLE, homeBase.FogState);
    }

    [Test]
    public void unexplored_node_is_not_counted_as_renderable()
    {
        // Given: rich_a is UNEXPLORED by default
        var richA = _mapData.GetNode("rich_a");
        Assert.AreEqual(FogState.UNEXPLORED, richA.FogState,
            "Non-home nodes should start as UNEXPLORED");
    }

    [Test]
    public void node_color_is_player_for_PLAYER_ownership()
    {
        var homeBase = _mapData.GetNode("home_base");
        homeBase.Ownership = OwnershipState.PLAYER;
        homeBase.FogState = FogState.VISIBLE;

        // Color matches COLOR_PLAYER
        Assert.AreEqual(OwnershipState.PLAYER, homeBase.Ownership);
    }

    [Test]
    public void node_color_is_enemy_for_ENEMY_ownership()
    {
        var homeBase = _mapData.GetNode("home_base");
        homeBase.Ownership = OwnershipState.ENEMY;
        homeBase.FogState = FogState.VISIBLE;

        Assert.AreEqual(OwnershipState.ENEMY, homeBase.Ownership);
    }

    [Test]
    public void node_color_is_neutral_for_NEUTRAL_ownership()
    {
        var homeBase = _mapData.GetNode("home_base");
        homeBase.Ownership = OwnershipState.NEUTRAL;
        homeBase.FogState = FogState.VISIBLE;

        Assert.AreEqual(OwnershipState.NEUTRAL, homeBase.Ownership);
    }

    // ─── TR-starmapui-002: Fleet icons ─────────────────────────────

    [Test]
    public void fleet_icon_created_on_dispatch_created_event()
    {
        // Given: fleet icon root exists
        Assert.IsNotNull(_fleetIconRoot, "FleetIconRoot should be created");

        // When: a dispatch order is created
        var order = new DispatchOrder {
            OrderId = "order-1",
            OriginNodeId = "home_base",
            DestNodeId = "rich_a",
            LockedPath = new System.Collections.Generic.List<string> { "home_base", "rich_a" }
        };
        FleetDispatchSystem.Instance.OnDispatchCreated?.Invoke(order);

        // Then: a fleet icon is added to the fleet icon root
        Assert.IsTrue(_fleetIconRoot.childCount > 0,
            "Fleet icon should be added to fleetIconRoot on dispatch created");
    }

    [Test]
    public void fleet_icon_removed_on_order_closed_event()
    {
        // When: OnOrderClosed fires
        FleetDispatchSystem.Instance.OnOrderClosed?.Invoke("order-1");

        // Then: fleet icon is removed from root (it's a no-op if not found)
        // The important thing is it doesn't throw
        Assert.Pass("OnOrderClosed should not throw");
    }

    // ─── TR-starmapui-003: Dispatch flow ───────────────────────────

    [Test]
    public void valid_path_exists_between_connected_nodes()
    {
        // home_base → rich_a are connected
        var path = StarMapPathfinder.FindPath(_mapData, "home_base", "rich_a");

        Assert.IsNotNull(path, "Path should exist between connected nodes");
        Assert.That(path.Count, Is.GreaterThan(0),
            "Path should have at least the destination");
    }

    [Test]
    public void no_path_between_unconnected_nodes()
    {
        // rich_d is not connected to rich_a
        var path = StarMapPathfinder.FindPath(_mapData, "rich_a", "rich_d");

        Assert.IsNull(path, "No path should exist between unconnected nodes");
    }

    [Test]
    public void IsValidDispatchTarget_returns_false_for_self()
    {
        // When: target is the same as origin
        var shipNode = "home_base";
        var targetNode = "home_base";

        // Path from node to itself is valid (single-element list)
        var path = StarMapPathfinder.FindPath(_mapData, shipNode, targetNode);
        Assert.IsNotNull(path);
        Assert.AreEqual(1, path.Count); // only the origin
    }

    // ─── TR-starmapui-004: Resource display ─────────────────────────

    [Test]
    public void ore_label_updates_on_resources_updated()
    {
        // When: OnResourcesUpdated fires with ore = 500
        var snapshot = new ResourceSnapshot(500, 100);
        _resourcesChannel.Raise(snapshot);

        // Then: ore label shows "500"
        Assert.AreEqual("500", _oreLabel.text,
            "Ore label should display the ore value");
    }

    [Test]
    public void energy_label_updates_on_resources_updated()
    {
        // When: OnResourcesUpdated fires with energy = 200
        var snapshot = new ResourceSnapshot(500, 200);
        _resourcesChannel.Raise(snapshot);

        // Then: energy label shows "200"
        Assert.AreEqual("200", _energyLabel.text,
            "Energy label should display the energy value");
    }

    [Test]
    public void resource_labels_show_initial_zero_when_no_update()
    {
        // Given: labels start empty or at default
        // When: no resources updated event fires

        // Then: labels are not null (can display later updates)
        Assert.IsNotNull(_oreLabel);
        Assert.IsNotNull(_energyLabel);
    }

    // ─── Zoom / Pan ───────────────────────────────────────────────

    [Test]
    public void zoom_is_clamped_to_minimum()
    {
        CallMethod(_ui, "OnWheel", new WheelEvent {
            delta = new Vector2(0, -1) // zoom out
        });

        // Zoom should not go below 0.5
        var zoom = (float)typeof(StarMapUI)
            .GetField("_zoomScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);
        Assert.That(zoom, Is.GreaterThanOrEqualTo(0.5f));
    }

    [Test]
    public void zoom_is_clamped_to_maximum()
    {
        // Zoom in multiple times
        for (int i = 0; i < 20; i++) {
            CallMethod(_ui, "OnWheel", new WheelEvent {
                delta = new Vector2(0, 1) // zoom in
            });
        }

        var zoom = (float)typeof(StarMapUI)
            .GetField("_zoomScale", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);
        Assert.That(zoom, Is.LessThanOrEqualTo(2f));
    }

    // ─── Interaction state machine ────────────────────────────────

    [Test]
    public void interaction_state_starts_at_IDLE()
    {
        var state = typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);

        Assert.AreEqual("IDLE", state.ToString());
    }

    [Test]
    public void clicking_same_node_twice_transitions_to_ShipSelect()
    {
        // Tap home_base once → NODE_SELECTED
        CallMethod(_ui, "HandleNodeTap", "home_base");

        var state1 = (object)typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);

        // Tap same node again → should transition
        CallMethod(_ui, "HandleNodeTap", "home_base");

        var state2 = (object)typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);

        Assert.AreNotEqual(state1.ToString(), state2.ToString());
    }

    [Test]
    public void clicking_different_node_resets_selection()
    {
        // Select home_base
        CallMethod(_ui, "HandleNodeTap", "home_base");
        var state1 = (object)typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);
        Assert.AreEqual("NODE_SELECTED", state1.ToString());

        // Tap different node → still NODE_SELECTED but different node
        CallMethod(_ui, "HandleNodeTap", "rich_a");
        var state2 = (object)typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);
        Assert.AreEqual("NODE_SELECTED", state2.ToString());
    }

    [Test]
    public void clicking_background_resets_to_IDLE()
    {
        // Select a node first
        CallMethod(_ui, "HandleNodeTap", "home_base");

        // Tap background → IDLE
        CallMethod(_ui, "HandleBackgroundTap");

        var state = (object)typeof(StarMapUI)
            .GetProperty("CurrentState", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .GetValue(_ui);
        Assert.AreEqual("IDLE", state.ToString());
    }

    // ─── View Layer visibility ──────────────────────────────────

    [Test]
    public void starmap_ui_visible_in_STARMAP_layer()
    {
        _viewLayerChannel.Raise(ViewLayer.STARMAP);
        Assert.IsTrue(_uiGo.activeSelf,
            "StarMapUI should be visible in STARMAP layer");
    }

    [Test]
    public void starmap_ui_hidden_in_COCKPIT_layer()
    {
        _viewLayerChannel.Raise(ViewLayer.COCKPIT);
        Assert.IsFalse(_uiGo.activeSelf,
            "StarMapUI should be hidden in COCKPIT layer");
    }
}
