using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplineMover : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 10f;
    public bool loop = true;

    // 디버그용 (Inspector에서 확인 가능)
    public float currentDistance;
    public float trackLength;

    void Start()
    {
        if (splineContainer == null) return;
        trackLength = splineContainer.CalculateLength();
    }

    void Update()
    {
        if (splineContainer == null) return;

        // 1. 거리 이동
        currentDistance += speed * Time.deltaTime;
        if (currentDistance > trackLength)
        {
            if (loop) currentDistance -= trackLength;
            else currentDistance = trackLength;
        }

        // 2. 위치 계산 (NativeSpline 이용 - 스케일 문제 해결사)
        MoveCarUsingNative(currentDistance);
    }

    void MoveCarUsingNative(float distance)
    {
        float t = distance / trackLength;

        // [핵심] NativeSpline을 사용하여 월드 좌표 변환을 더 정확하게 처리
        using (var native = new NativeSpline(splineContainer.Spline, splineContainer.transform.localToWorldMatrix))
        {
            // 위치와 방향을 한방에 월드 좌표로 뽑아냄
            float3 pos;
            float3 tangent;
            float3 up;

            native.Evaluate(t, out pos, out tangent, out up);

            transform.position = pos; // 월드 위치 적용

            if (!tangent.Equals(float3.zero))
            {
                transform.rotation = Quaternion.LookRotation(tangent, up);
            }
        }
    }
}