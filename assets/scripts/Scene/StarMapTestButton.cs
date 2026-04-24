using UnityEngine;
using Game.Scene;

namespace Game.Scene
{
    /// <summary>
    /// Temporary OnGUI button for Alpha vertical slice testing.
    /// Displays "Enter Cockpit" button in star map view.
    /// Remove once StarMapUI is fully wired.
    /// </summary>
    public class StarMapTestButton : MonoBehaviour
    {
        private void OnGUI()
        {
            if (ViewLayerManager.Instance == null) return;
            if (ViewLayerManager.Instance.CurrentViewLayer != ViewLayer.STARMAP) return;

            // 中央标题
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "STAR MAP — Alpha Test", titleStyle);

            // 提示文字
            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40), "Click the button below to enter cockpit combat", hintStyle);

            // 大按钮
            float buttonW = 350f;
            float buttonH = 80f;
            float x = (Screen.width - buttonW) / 2f;
            float y = Screen.height * 0.6f;

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };

            if (GUI.Button(new Rect(x, y, buttonW, buttonH), "ENTER COCKPIT", buttonStyle))
            {
                ViewLayerManager.Instance.RequestEnterCockpit("player_001");
            }
        }
    }
}
