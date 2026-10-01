#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using VRKart.Core;

namespace VRKart.Race.Testing
{
    // PC 테스트용 키보드 운전 모드 (에디터·PC 빌드에서만, Quest 빌드에는 없음).
    // 헤드셋이 연결돼 있지 않으면 자동으로 켜지고, XR Interaction Simulator를 꺼서 키가 겹치지 않게 한다.
    // 키 하나 = 기능 하나: T 가속, Shift 브레이크, R 후진, J 좌회전, L 우회전, Space 드리프트, E 아이템, P 일시정지(Esc는 에디터가 가로챔).
    // F1 = 키보드 운전 ↔ 시뮬레이터(핸들 잡기 테스트) 전환. 씬에 따로 놓을 필요 없이 자동으로 생긴다.
    public sealed class DesktopDriveController : MonoBehaviour
    {
        private const float HintWidth = 300f;

        private bool _headsetConnected;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(DesktopDriveController)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DesktopDriveController>();
        }

        private void Start()
        {
            _headsetConnected = XRSettings.isDeviceActive;
            DesktopDriveMode.Active = !_headsetConnected;
        }

        private void OnDestroy() => DesktopDriveMode.Active = false;

        private void Update()
        {
            if (_headsetConnected) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) DesktopDriveMode.Active = !DesktopDriveMode.Active;

            // 시뮬레이터는 XRI가 실행 중에 만들 수 있어서 매 프레임 확인한다
            XRInteractionSimulator simulator = XRInteractionSimulator.instance;
            if (simulator != null && simulator.enabled == DesktopDriveMode.Active) simulator.enabled = !DesktopDriveMode.Active;

            if (DesktopDriveMode.Active && keyboard.pKey.wasPressedThisFrame)
            {
                RaceManager raceManager = FindAnyObjectByType<RaceManager>();
                if (raceManager != null) raceManager.TogglePause();
            }
        }

        private void OnGUI()
        {
            if (_headsetConnected) return;
            _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, wordWrap = true };
            string text = DesktopDriveMode.Active
                ? "PC 운전 모드 (F1 = 시뮬레이터로)\nT 가속   Shift 브레이크   R 후진\nJ 좌회전   L 우회전\nSpace 드리프트   E 아이템   P 일시정지\n메뉴 버튼은 마우스로 클릭"
                : "시뮬레이터 모드 (F1 = PC 운전으로)\nXR Interaction Simulator 키 사용";
            GUI.Box(new Rect(10f, 10f, HintWidth, DesktopDriveMode.Active ? 96f : 44f), text, _style);
        }
    }
}
#endif
