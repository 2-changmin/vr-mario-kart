using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRKart.Race
{
    // 씬 전환 페이드 (#66). 화면을 어둡게 → 비동기 로드 → 새 씬에서 밝게.
    // SceneManager.LoadScene은 로드 중 VR 화면을 멈추게 해서(머리를 돌려도 화면이 안 따라옴) 멀미를 부른다 →
    // 검은 화면인 동안 LoadSceneAsync로 불러온다. SceneLoader.Load가 실행 중이면 자동으로 이것을 쓴다.
    // 카메라(Camera.main) 바로 앞에 쿼드를 붙여 그린다(ScreenFade 셰이더: 깊이 무시, 양쪽 눈). 씬이 바뀌어도 살아 있다(DontDestroyOnLoad).
    // 카메라는 움직이지 않는다(NFR-03) — 화면 밝기만 바뀐다.
    public sealed class SceneFader : MonoBehaviour
    {
        private const float FadeOutSeconds = 0.3f;
        private const float FadeInSeconds = 0.45f;
        private const float QuadDistance = 0.12f;

        private static SceneFader s_instance;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Material _material;
        private MeshRenderer _renderer;
        private Coroutine _routine;
        private float _alpha;
        private bool _loading;

        // 어두워지는 중이거나 로드 중이면 true (밝아지는 중에는 새 로드를 받는다)
        public static bool IsLoading => s_instance != null && s_instance._loading;

        public static void Load(string sceneName)
        {
            if (s_instance == null)
            {
                var go = new GameObject("SceneFader");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<SceneFader>();
            }
            if (s_instance._loading) return;   // 이미 다른 씬으로 가는 중
            if (s_instance._routine != null) s_instance.StopCoroutine(s_instance._routine);
            s_instance._routine = s_instance.StartCoroutine(s_instance.Run(sceneName));
        }

        private void Awake()
        {
            var shader = Shader.Find("VRKart/ScreenFade");
            var template = Resources.Load<Material>("ScreenFade");
            _material = template != null ? new Material(template) : new Material(shader);

            var quad = new GameObject("FadeQuad");
            quad.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "FadeQuad" };
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);   // 잘려서 안 그려지는 일 없게
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            _renderer = quad.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            quad.transform.localScale = Vector3.one * 2f;   // 0.12m 앞에서 4m × 4m → 시야 전체
            SetAlpha(0f);
        }

        private IEnumerator Run(string sceneName)
        {
            _loading = true;
            yield return Fade(1f, FadeOutSeconds);
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone) yield return null;
            yield return null;   // 새 씬의 XR 카메라가 자리 잡을 때까지 한 프레임
            _loading = false;
            yield return Fade(0f, FadeInSeconds);
            _routine = null;
        }

        private IEnumerator Fade(float target, float seconds)
        {
            float start = _alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)   // 일시정지(timeScale 0) 중에도
            {
                SetAlpha(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAlpha(target);
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            _material.SetColor(ColorId, new Color(0f, 0f, 0f, alpha));
            _renderer.enabled = alpha > 0.001f;
        }

        private void LateUpdate()
        {
            if (!_renderer.enabled) return;
            var camera = Camera.main;
            if (camera == null) return;
            var t = camera.transform;
            _renderer.transform.SetPositionAndRotation(t.position + t.forward * QuadDistance, t.rotation);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_material != null) Destroy(_material);
        }
    }
}
