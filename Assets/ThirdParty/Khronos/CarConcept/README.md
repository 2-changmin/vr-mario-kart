# Car Concept (Khronos glTF Sample Assets)

- 원본: https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CarConcept (`CarConcept.glb`)
- 라이선스: **CC BY 4.0 International** — created by Eric Chadwick of Darmstadt Graphics Group GmbH, 2024
- 표기 문구: "Car Concept" by Eric Chadwick (Darmstadt Graphics Group GmbH), CC BY 4.0 — modified

## 바꾼 것 (CC BY 4.0의 "변경 표시")

- GLB를 에디터 스크립트로 Unity 머티리얼(URP Lit/Unlit)·PNG 텍스처로 변환. 이 폴더에는 게임에서 쓰는 머티리얼 14개·텍스처 4장만 남김(파일 이름의 번호 = GLB 안 이미지 순서).
- **Khronos 로고·3D Commerce 로고(상표, 라이선스 대상 아님)는 넣지 않음**: 로고가 그려진 번호판 텍스처와 타이어 옆면 색·노멀 텍스처는 빼고, 번호판은 메시에서도 뺐고 타이어는 단색 고무 머티리얼(`_Project/Materials/Cars/CarConcept_Rubber`)로 바꿈.
- 유리는 Quest 성능 때문에 반투명을 쓰지 않음(플레이어 차는 유리 없음, AI 차는 `CarConcept_GlassDark`). 브레이크 캘리퍼·와이퍼·엔진·차축은 뺌.
- 메시는 머티리얼별로 합치고 간소화해서 `Assets/_Project/Models/CarConcept/CarConcept_Game.asset`에 저장(플레이어 실내·외형, AI 외형, 바퀴, 핸들, 그림자 대역).
