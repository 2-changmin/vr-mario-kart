using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace VRKart.Core
{
    // PC 시연 화질 (#67). 품질 단계가 "PC"일 때만 씬마다 전역 후처리 볼륨(Resources/PC_PostProcess: 톤 매핑·블룸·색 보정)을 만들고
    // 카메라의 후처리·안티에일리어싱을 켠다. Quest(Mobile 단계)에서는 아무것도 하지 않는다 → 씬·프리팹 수정 없음.
    // 모션 블러·화면 흔들림·FOV 변화는 넣지 않는다(NFR-03). 그림자·주변 음영(SSAO)은 PC_RPAsset / PC_Renderer에 있다.
    // 에디터에서 켜기: 메뉴 VRKart > 화질 > PC 시연 화질.
    public static class PcShowcaseQuality
    {
        private const string VolumeName = "PC Showcase Volume";

        public static bool IsPcTier => QualitySettings.names[QualitySettings.GetQualityLevel()] == "PC";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (_, _) => Apply();
            Apply();
        }

        public static void Apply()
        {
            if (!IsPcTier) return;
            if (GameObject.Find(VolumeName) == null)
            {
                var profile = Resources.Load<VolumeProfile>("PC_PostProcess");
                if (profile == null) return;
                var volume = new GameObject(VolumeName).AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) EnablePostProcessing(camera);
        }

        public static void EnablePostProcessing(Camera camera)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
