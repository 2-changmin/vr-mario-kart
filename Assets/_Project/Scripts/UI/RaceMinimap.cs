using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VRKart.AI;
using VRKart.Race;
using VRKart.Track;

namespace VRKart.UI
{
    // 미니맵 (#48): 트랙 전체 모양 + 내 카트(방향 화살표) + 다른 카트(색 점). 북쪽이 위로 고정.
    // RaceHud가 실행 중에 만들어 조종석의 미니맵 자리(CockpitHudAnchors.Map, 앞유리 오른쪽 위)에 붙인다 → 씬마다 따로 놓을 필요 없음.
    // 트랙 모양은 TrackLayout 중심선, 없으면 AI WaypointPath에서 가져온다.
    public sealed class RaceMinimap : MonoBehaviour
    {
        private const int TextureSize = 256;
        private const float PanelSize = 300f;    // 캔버스 픽셀 (스케일 0.001 → 앵커 스케일과 곱해 약 16cm)
        private const float MapInset = 18f;

        private readonly List<(Transform kart, RectTransform marker, bool player)> _markers = new List<(Transform, RectTransform, bool)>();
        private Vector2 _center;
        private float _extent;
        private float _mapSize;
        private Texture2D _texture;

        // anchor 자식으로 미니맵 캔버스를 만든다. worldScale: 앵커 없이 HUD 안에 띄울 때 크기
        public static RaceMinimap Create(Transform parent, RaceManager raceManager, float worldScale = 0.001f)
        {
            var go = new GameObject("Minimap", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(PanelSize, PanelSize);
            rect.localScale = Vector3.one * worldScale;
            var minimap = go.AddComponent<RaceMinimap>();
            minimap.Build(raceManager);
            return minimap;
        }

        private void Build(RaceManager raceManager)
        {
            List<Vector3> path = TrackPoints();
            if (path.Count < 2) { gameObject.SetActive(false); return; }

            Vector2 min = new Vector2(path.Min(p => p.x), path.Min(p => p.z));
            Vector2 max = new Vector2(path.Max(p => p.x), path.Max(p => p.z));
            _center = (min + max) * 0.5f;
            _extent = Mathf.Max(max.x - min.x, max.y - min.y) * 1.08f;
            _mapSize = PanelSize - MapInset * 2f;

            var background = NewImage("Background", transform, new Color(0.05f, 0.07f, 0.1f, 0.75f));
            background.rectTransform.sizeDelta = new Vector2(PanelSize, PanelSize);

            _texture = DrawTrack(path);
            var mapGo = new GameObject("Track", typeof(RectTransform));
            mapGo.transform.SetParent(transform, false);
            var raw = mapGo.AddComponent<RawImage>();
            raw.texture = _texture;
            raw.raycastTarget = false;
            raw.rectTransform.sizeDelta = new Vector2(_mapSize, _mapSize);

            // 카트 표시: 내 카트는 흰 화살표(맨 위), 다른 카트는 차 색 점
            Sprite dot = MakeSprite(Circle), arrow = MakeSprite(Arrow);
            Transform player = raceManager != null && raceManager.Player != null ? raceManager.Player.transform : null;
            int index = 0;
            foreach (var progress in FindObjectsByType<RaceProgress>(FindObjectsSortMode.InstanceID).OrderBy(p => p.transform == player ? 1 : 0))
            {
                bool isPlayer = progress.transform == player;
                var image = NewImage(isPlayer ? "Me" : progress.name, transform, isPlayer ? Color.white : KartColor(progress.transform, index++));
                image.sprite = isPlayer ? arrow : dot;
                image.rectTransform.sizeDelta = Vector2.one * (isPlayer ? 26f : 18f);
                if (isPlayer)
                {
                    var outline = image.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0.1f, 0.35f, 0.9f);
                    outline.effectDistance = new Vector2(2f, -2f);
                }
                _markers.Add((progress.transform, image.rectTransform, isPlayer));
            }
        }

        private void LateUpdate()
        {
            foreach (var (kart, marker, player) in _markers)
            {
                if (kart == null) continue;
                marker.anchoredPosition = ToMap(kart.position);
                if (player) marker.localRotation = Quaternion.Euler(0f, 0f, -kart.eulerAngles.y);   // 북쪽이 위, 화살표 = 진행 방향
            }
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private Vector2 ToMap(Vector3 world) =>
            new Vector2((world.x - _center.x) / _extent * _mapSize, (world.z - _center.y) / _extent * _mapSize);

        private static List<Vector3> TrackPoints()
        {
            var layout = FindAnyObjectByType<TrackLayout>();
            if (layout != null && layout.Samples.Count > 1) return layout.Samples.Select(s => s.Position).ToList();
            var path = FindAnyObjectByType<WaypointPath>();
            var points = new List<Vector3>();
            if (path != null) for (int i = 0; i < path.Count; i++) points.Add(path.GetPoint(i));
            return points;
        }

        // 트랙 선: 바깥 어두운 테두리 + 안쪽 밝은 선. 출발·결승은 흰 칸
        private Texture2D DrawTrack(List<Vector3> path)
        {
            var pixels = new Color32[TextureSize * TextureSize];
            Vector2 ToTex(Vector3 p) => (ToMap(p) / _mapSize + Vector2.one * 0.5f) * (TextureSize - 1);
            void Stamp(Vector2 c, float radius, Color32 color)
            {
                int r = Mathf.CeilToInt(radius);
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + y * y > radius * radius) continue;
                        int px = Mathf.RoundToInt(c.x) + x, py = Mathf.RoundToInt(c.y) + y;
                        if (px < 0 || py < 0 || px >= TextureSize || py >= TextureSize) continue;
                        pixels[py * TextureSize + px] = color;
                    }
            }
            void Line(float radius, Color32 color)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 a = ToTex(path[i]), b = ToTex(path[(i + 1) % path.Count]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
                    for (int k = 0; k <= steps; k++) Stamp(Vector2.Lerp(a, b, (float)k / steps), radius, color);
                }
            }
            Line(4.2f, new Color32(20, 24, 30, 255));
            Line(2.6f, new Color32(225, 228, 235, 255));
            var layout = FindAnyObjectByType<TrackLayout>();
            if (layout != null)
            {
                var finish = layout.Evaluate(layout.FinishDistance);
                Vector2 f = ToTex(finish.Position), side = ToTexDir(finish.Right) * 5f;
                for (float t = -1f; t <= 1f; t += 0.1f) Stamp(f + side * t, 1.3f, new Color32(255, 70, 70, 255));
            }
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;

            Vector2 ToTexDir(Vector3 d) => new Vector2(d.x, d.z).normalized;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // AI 차 색: 차체 리버리 머티리얼 이름(Red/Yellow/Green…)으로, 없으면 순서대로
        private static Color KartColor(Transform kart, int index)
        {
            foreach (var r in kart.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name;
                    if (n.Contains("Red")) return new Color(0.95f, 0.25f, 0.2f);
                    if (n.Contains("Yellow")) return new Color(1f, 0.82f, 0.15f);
                    if (n.Contains("Green")) return new Color(0.25f, 0.85f, 0.35f);
                }
            Color[] palette = { new Color(0.95f, 0.25f, 0.2f), new Color(1f, 0.82f, 0.15f), new Color(0.25f, 0.85f, 0.35f), new Color(0.3f, 0.8f, 1f) };
            return palette[index % palette.Length];
        }

        private static float Circle(float x, float y) => x * x + y * y <= 1f ? 1f : 0f;

        // 위(+y)를 향한 화살표
        private static float Arrow(float x, float y)
        {
            if (y < -0.85f || y > 0.95f) return 0f;
            float halfWidth = (0.95f - y) / 1.8f * 0.9f;
            if (Mathf.Abs(x) > halfWidth) return 0f;
            return y < -0.35f && Mathf.Abs(x) < (y + 0.85f) * 0.9f ? 0f : 1f;   // 아래쪽 홈
        }

        private static Sprite MakeSprite(System.Func<float, float, float> shape)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(shape(u, v) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }
    }
}
