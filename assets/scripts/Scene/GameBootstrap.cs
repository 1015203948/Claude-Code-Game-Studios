using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Game.Data;
using Game.Channels;
using Game.Gameplay;
using Game.UI;
using Gameplay;

namespace Game.Scene
{
    /// <summary>
    /// Game bootstrap — initializes GameDataManager, StarMapData, player ship,
    /// and channel references. Attach to "Bootstrap" GameObject in MasterScene.
    ///
    /// Execution order: runs before all other scripts (-100) to ensure
    /// GameDataManager.Instance is available when other components' Awake/OnEnable fires.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Channel SO Assets (auto-loaded from Resources if not assigned)")]
        [SerializeField] private ShipStateChannel _shipStateChannel;
        [SerializeField] private ViewLayerChannel _viewLayerChannel;
        [SerializeField] private CombatChannel _combatChannel;

        [Header("Game Config")]
        [SerializeField] private ShipBlueprint _playerBlueprint;
        [SerializeField] private HullBlueprint _playerHull;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            // Step 1: Initialize GameDataManager singleton
            if (GameDataManager.Instance == null)
            {
                new GameDataManager();
                Debug.Log("[GameBootstrap] GameDataManager initialized.");
            }

            // Step 2: Load channels from Assets/data/ if not assigned in Inspector
            if (_shipStateChannel == null)
                _shipStateChannel = LoadAsset<ShipStateChannel>("Assets/data/channels/ShipStateChannel.asset");
            if (_viewLayerChannel == null)
                _viewLayerChannel = LoadAsset<ViewLayerChannel>("Assets/data/channels/ViewLayerChannel.asset");
            if (_combatChannel == null)
                _combatChannel = LoadAsset<CombatChannel>("Assets/data/channels/CombatChannel.asset");

            // Step 3: Initialize StarMapData with default nodes
            InitializeStarMapData();

            // Step 4: Register player ship
            RegisterPlayerShip();

            // Step 5: Load StarMapScene additively (ADR-0001: MasterScene + StarMapScene always loaded)
            LoadStarMapSceneAdditive();
        }

        private async void LoadStarMapSceneAdditive() {
            try {
                // 等一帧让 SceneManager 注册编辑器预加载的场景
                await Task.Yield();

                // 跳过已在 Editor 中预加载的场景，避免重复
                if (SceneManager.GetSceneByName("StarMapScene").isLoaded) {
                    Debug.Log("[GameBootstrap] StarMapScene already loaded, skipping additive load.");
                    return;
                }

                var op = SceneManager.LoadSceneAsync("StarMapScene", LoadSceneMode.Additive);
                if (op != null) {
                    while (!op.isDone) {
                        await Task.Yield();
                    }
                    Debug.Log("[GameBootstrap] StarMapScene loaded additively.");

                    // Disable duplicate cameras from StarMapScene — MasterScene already has them
                    var starMapScene = SceneManager.GetSceneByName("StarMapScene");
                    foreach (var root in starMapScene.GetRootGameObjects()) {
                        foreach (var cam in root.GetComponentsInChildren<Camera>()) {
                            if (cam.CompareTag("MainCamera")) continue; // Don't disable cockpit camera
                            cam.enabled = false;
                            Debug.Log($"[GameBootstrap] Disabled duplicate camera: {cam.name}");
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.LogWarning($"[GameBootstrap] Could not load StarMapScene: {ex.Message}");
            }
        }

        private void Start()
        {
            // Wire channel references after all OnEnable have fired
            WireChannelReferences();

            // Load persisted colony resources (after StarMapScene is loaded)
            var colony = ColonyManager.Instance;
            if (colony != null) {
                if (!colony.Load()) {
                    colony.Initialize(100, 50);
                }
            }
        }

        // ─── Star Map Initialization (Sprint 1 Task M1) ──────────────────

        private void InitializeStarMapData()
        {
            var mapData = StarMapData.CreateMvpDiamond();
            GameDataManager.Instance.SetStarMapData(mapData);
            Debug.Log("[GameBootstrap] StarMapData initialized with MVP diamond layout.");
        }

        // ─── Player Ship Registration ─────────────────────────────────────

        private void RegisterPlayerShip()
        {
            if (_playerBlueprint == null)
                _playerBlueprint = LoadAsset<ShipBlueprint>("Assets/data/config/ShipBlueprint_generic_v1.asset");

            if (_playerHull == null)
                _playerHull = LoadAsset<HullBlueprint>("Assets/data/Hulls/Hull_Fighter.asset");

            if (_playerBlueprint == null)
            {
                Debug.LogError("[GameBootstrap] No player blueprint found — skipping ship registration.");
                return;
            }

            var ship = new ShipDataModel(
                instanceId: "player_001",
                blueprintId: _playerBlueprint.BlueprintId,
                isPlayerControlled: true,
                blueprint: _playerHull,
                shipStateChannel: _shipStateChannel
            );

            ship.DockedNodeId = "home_base";

            GameDataManager.Instance.RegisterShip(ship);
            Debug.Log($"[GameBootstrap] Player ship registered: {ship.InstanceId} (Hull={ship.MaxHull})");
        }

        // ─── Channel Reference Wiring ─────────────────────────────────────

        private void WireChannelReferences()
        {
            // ViewLayerManager
            if (ViewLayerManager.Instance != null)
            {
                if (_viewLayerChannel != null)
                    SetField(ViewLayerManager.Instance, "_viewLayerChannel", _viewLayerChannel);
                if (_shipStateChannel != null)
                    SetField(ViewLayerManager.Instance, "_shipStateChannel", _shipStateChannel);
            }

            // ShipControlSystem
            if (ShipControlSystem.Instance != null)
            {
                if (_shipStateChannel != null)
                    SetField(ShipControlSystem.Instance, "_shipStateChannel", _shipStateChannel);
                if (_viewLayerChannel != null)
                    SetField(ShipControlSystem.Instance, "_viewLayerChannel", _viewLayerChannel);
            }

            // ShipHUD
            var hud = FindAnyObjectByType<ShipHUD>();
            if (hud != null)
            {
                if (_viewLayerChannel != null)
                    SetField(hud, "_viewLayerChannel", _viewLayerChannel);
                if (_shipStateChannel != null)
                    SetField(hud, "_shipStateChannel", _shipStateChannel);
            }

            // StarMapOverlayController
            var overlay = FindAnyObjectByType<StarMapOverlayController>();
            if (overlay != null && _viewLayerChannel != null)
                SetField(overlay, "_viewLayerChannel", _viewLayerChannel);

            Debug.Log("[GameBootstrap] Channel references wired.");
        }

        // ─── Utility ──────────────────────────────────────────────────────

        private static T LoadAsset<T>(string assetPath) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
#else
            Debug.LogWarning($"[GameBootstrap] Cannot load {assetPath} at runtime — use Addressables.");
            return null;
#endif
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
            else
                Debug.LogWarning($"[GameBootstrap] Field '{fieldName}' not found on {target.GetType().Name}.");
        }

        /// <summary>
        /// Wires CockpitScene components with channel references.
        /// Called by ViewLayerManager after CockpitScene is loaded.
        /// </summary>
        public void WireCockpitComponents(ShipControlSystem scs)
        {
            if (scs == null) return;

            if (_shipStateChannel != null)
                SetField(scs, "_shipStateChannel", _shipStateChannel);
            if (_viewLayerChannel != null)
                SetField(scs, "_viewLayerChannel", _viewLayerChannel);

            Debug.Log("[GameBootstrap] Cockpit ShipControlSystem channels wired.");
        }
    }
}
