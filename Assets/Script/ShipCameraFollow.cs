using UnityEngine;

public class ShipCameraFollow : MonoBehaviour
{
    public Transform target; // 따라다닐 배 (Ship)

    public Vector3 offset = new Vector3(0, 10, -30); // 카메라 위치 (위, 뒤)
    public float smoothSpeed = 5f; // 따라가는 속도 (부드러움)

    void FixedUpdate()
    {
        if (target == null) return;

        // 1. 배의 "위치"는 따라가되, 배의 "기울기(파도)"는 무시하고 "방향(Y축)"만 가져옴
        // (배가 파도를 타서 앞코가 들려도 카메라는 수평을 유지함)
        Quaternion targetRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);

        // 2. 목표 위치 계산: 배 위치 + (배가 보는 방향 * 떨어진 거리)
        Vector3 desiredPosition = target.position + (targetRotation * offset);

        // 3. 부드럽게 이동 (Lerp)
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // 4. 카메라는 항상 배를 쳐다봄
        transform.LookAt(target);
    }
}