using UnityEngine;
using UnityEngine.UI; // UI를 다루기 위해 필요

public class SimpleMinimap : MonoBehaviour
{
    [Header("1. 타겟 연결")]
    public Transform carTransform; // 실제 자동차

    [Header("2. UI 연결")]
    public RectTransform mapContainerRect; // 지도의 배경 (MapContainer)
    public RectTransform carDotRect;       // 빨간 점 (CarDot)

    [Header("3. 월드 경계 연결")]
    public Transform worldBoundMin; // 아까 만든 MapBound_Min
    public Transform worldBoundMax; // 아까 만든 MapBound_Max

    private Vector2 mapSize;

    void Start()
    {
        // 시작할 때 지도 컨테이너의 UI 크기를 미리 계산해둡니다.
        mapSize = new Vector2(mapContainerRect.rect.width, mapContainerRect.rect.height);
    }

    void Update()
    {
        if (carTransform == null) return;

        // 1. 현재 차의 위치를 가져옵니다.
        Vector3 carPos = carTransform.position;

        // 2. 차의 위치가 월드 경계(Min ~ Max) 사이에서 몇 퍼센트(0.0 ~ 1.0) 지점인지 계산합니다.
        // Mathf.InverseLerp(a, b, value) : value가 a와 b 사이에서 어디쯤인지 0~1로 반환
        float normalizedX = Mathf.InverseLerp(worldBoundMin.position.x, worldBoundMax.position.x, carPos.x);
        float normalizedZ = Mathf.InverseLerp(worldBoundMin.position.z, worldBoundMax.position.z, carPos.z);

        // 3. (옵션) 점이 지도를 벗어나지 않게 0과 1 사이로 가둡니다.
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedZ = Mathf.Clamp01(normalizedZ);

        // 4. 계산된 퍼센트를 UI 크기에 곱해서 실제 UI 좌표를 구합니다.
        // 3D의 Z축이 2D UI에서는 Y축(높이)이 됩니다.
        Vector2 mappedPos = new Vector2(normalizedX * mapSize.x, normalizedZ * mapSize.y);

        // 5. 빨간 점의 위치를 업데이트합니다.
        carDotRect.anchoredPosition = mappedPos;
    }
}