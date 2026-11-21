#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VRKart.Core
{
    // PC 운전 모드(헤드셋 없음)에서 메뉴·일시정지·결과 화면을 마우스로 누를 수 있게 한다 (#80).
    // 월드 공간 UI에는 XR 컨트롤러 레이저용 TrackedDeviceGraphicRaycaster만 있는데, 이 레이캐스터는 마우스 이벤트를 무시한다.
    // 그래서 같은 Canvas에 마우스용 GraphicRaycaster를 붙이고, PC 운전 모드일 때만 켠다(시뮬레이터·헤드셋에서는 꺼서 레이저와 겹치지 않게).
    // 씬이 바뀌거나 모드가 바뀔 때만 Canvas를 다시 찾는다. 에디터·PC 빌드에만 들어가고 Quest 빌드에는 없다.
    public sealed class DesktopUiMouse : MonoBehaviour
    {
        private Canvas[] _canvases = new Canvas[0];
        private bool _applied;
        private bool _dirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(DesktopUiMouse)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DesktopUiMouse>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;

        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => _dirty = true;

        private void LateUpdate()
        {
            bool active = DesktopDriveMode.Active;
            if (!_dirty && active == _applied) return;

            if (_dirty)
            {
                _canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                _dirty = false;
            }
            _applied = active;

            foreach (Canvas canvas in _canvases)
            {
                if (canvas == null || !canvas.TryGetComponent(out TrackedDeviceGraphicRaycaster _)) continue;
                if (!canvas.TryGetComponent(out GraphicRaycaster mouse)) mouse = canvas.gameObject.AddComponent<GraphicRaycaster>();
                mouse.enabled = active;
                if (active && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            }
        }
    }
}
#endif
