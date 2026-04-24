// ShipModelSetup.cs — Editor tool for configuring X4 ship models
// Place in Assets/Editor/

using System.Linq;
using UnityEngine;
using UnityEditor;

namespace Game.EditorTools {
    public class ShipModelSetup : EditorWindow {
        private GameObject _enemyShipPrefab;
        private GameObject _playerShipPrefab;
        private GameObject _fighterModel;
        private GameObject _destroyerModel;
        private float _modelScale = 1.0f;

        [MenuItem("Tools/Ship Model Setup")]
        public static void ShowWindow() {
            GetWindow<ShipModelSetup>("Ship Model Setup");
        }

        private void OnGUI() {
            GUILayout.Label("X4 Ship Model Configuration", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("Step 1: Import Models", EditorStyles.label);
            EditorGUILayout.HelpBox(
                "1. Copy .blend, .dae, or .glb files to Assets/Models/Ships/\n" +
                "2. Unity will auto-import them (requires Blender for .blend)\n" +
                "3. Drag the imported prefab/model into the slots below",
                MessageType.Info
            );
            GUILayout.Space(10);

            GUILayout.Label("Step 2: Assign Models", EditorStyles.label);
            _fighterModel = EditorGUILayout.ObjectField(
                "Fighter Model", _fighterModel, typeof(GameObject), false
            ) as GameObject;
            _destroyerModel = EditorGUILayout.ObjectField(
                "Destroyer Model", _destroyerModel, typeof(GameObject), false
            ) as GameObject;
            GUILayout.Space(5);
            _modelScale = EditorGUILayout.FloatField(
                "Model Scale", _modelScale
            );
            EditorGUILayout.HelpBox(
                "Tip: Use 0.01 for .blend files, 1.0 for .dae files (adjust if model looks too big/small)",
                MessageType.Info
            );
            GUILayout.Space(10);

            GUILayout.Label("Step 3: Assign Targets", EditorStyles.label);
            _enemyShipPrefab = EditorGUILayout.ObjectField(
                "EnemyShip Prefab", _enemyShipPrefab, typeof(GameObject), false
            ) as GameObject;
            _playerShipPrefab = EditorGUILayout.ObjectField(
                "PlayerShip (CockpitScene)", _playerShipPrefab, typeof(GameObject), true
            ) as GameObject;
            GUILayout.Space(20);

            GUI.enabled = _fighterModel != null && _enemyShipPrefab != null;
            if (GUILayout.Button("Configure EnemyShip (Fighter)", GUILayout.Height(30))) {
                ConfigureEnemyShip();
            }
            GUI.enabled = _fighterModel != null && _playerShipPrefab != null;
            if (GUILayout.Button("Configure PlayerShip (Fighter)", GUILayout.Height(30))) {
                ConfigurePlayerShip();
            }
            GUI.enabled = true;

            GUILayout.Space(20);
            EditorGUILayout.HelpBox(
                "Note: The fighter GLB was exported without materials due to a " +
                "Blender 4.2 GLTF export bug. Assign a simple material in Unity.\n\n" +
                "The destroyer GLB includes full materials.",
                MessageType.Warning
            );
        }

        private void ConfigureEnemyShip() {
            if (_enemyShipPrefab == null || _fighterModel == null) return;

            // Instantiate the prefab for editing
            string path = AssetDatabase.GetAssetPath(_enemyShipPrefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(path);

            // Remove old MeshFilter and MeshRenderer
            var meshFilter = instance.GetComponent<MeshFilter>();
            var meshRenderer = instance.GetComponent<MeshRenderer>();
            if (meshFilter != null) DestroyImmediate(meshFilter);
            if (meshRenderer != null) DestroyImmediate(meshRenderer);

            // Remove old model children
            foreach (Transform child in instance.transform.Cast<Transform>().ToArray()) {
                DestroyImmediate(child.gameObject);
            }

            // Instantiate model as child
            GameObject modelInstance = Instantiate(_fighterModel, instance.transform);
            modelInstance.name = "Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * _modelScale;

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            PrefabUtility.UnloadPrefabContents(instance);

            Debug.Log($"[ShipModelSetup] EnemyShip configured with fighter model. Path: {path}");
            EditorUtility.DisplayDialog("Success", "EnemyShip prefab updated!", "OK");
        }

        private void ConfigurePlayerShip() {
            if (_playerShipPrefab == null || _fighterModel == null) return;

            // For scene objects, we work directly
            GameObject ship = _playerShipPrefab;

            // Remove old model children
            foreach (Transform child in ship.transform.Cast<Transform>().ToArray()) {
                if (child.name == "Model") DestroyImmediate(child.gameObject);
            }

            // Instantiate model as child
            GameObject modelInstance = Instantiate(_fighterModel, ship.transform);
            modelInstance.name = "Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * _modelScale;

            Debug.Log("[ShipModelSetup] PlayerShip configured with fighter model.");
            EditorUtility.DisplayDialog("Success", "PlayerShip updated! Remember to save the scene.", "OK");
        }
    }
}
