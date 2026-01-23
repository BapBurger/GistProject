using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CarController : MonoBehaviour
{
    [Header("경로 설정")]
    public SplineContainer trackSpline; // 생성된 스플라인 연결

    [Header("주행 설정")]
    public float speed = 0f;          // 현재 속도 (m/s)
    public float maxSpeed = 30f;      // 최대 속도 (약 100km/h)
    public float acceleration = 10f;  // 가속도
    public float braking = 20f;       // 제동력

    [Header("실시간 정보 (읽기 전용)")]
    public float distance = 0f;       // 주행 거리 (0 ~ 1 정규화 아님, 미터 단위)
    public float currentCurvature;    // 현재 위치의 곡률
    public float lateralG;            // 횡가속도 (Sway 힘)
    public float longitudinalG;       // 전후가속도 (Surge 힘)

    private float trackLength;

    void Start()
    {
        if (trackSpline != null)
            trackLength = trackSpline.CalculateLength();
    }

    void Update()
    {
        if (trackSpline == null) return;

        // 1. 가감속 조작 (W/S 키)
        if (Input.GetKey(KeyCode.W))
            speed += acceleration * Time.deltaTime;
        else if (Input.GetKey(KeyCode.S))
            speed -= braking * Time.deltaTime;

        // 마찰력에 의한 자연 감속 (엑셀 떼면 천천히 멈춤)
        speed = Mathf.Lerp(speed, 0, Time.deltaTime * 0.5f);
        speed = Mathf.Clamp(speed, 0, maxSpeed); // 후진 없음, 최대 속도 제한

        // 2. 이동 (거리 = 속력 x 시간)
        distance += speed * Time.deltaTime;

        // 트랙 끝에 다다르면 처음으로 (뺑뺑이)
        if (distance > trackLength) distance -= trackLength;

        // 3. 스플라인 위의 위치(t) 구하기 (0.0 ~ 1.0)
        float t = distance / trackLength;

        // 4. 위치와 회전 적용
        Vector3 position = trackSpline.EvaluatePosition(t);
        Vector3 tangent = trackSpline.EvaluateTangent(t);
        Vector3 up = trackSpline.EvaluateUpVector(t);

        transform.position = position;
        transform.rotation = Quaternion.LookRotation(tangent, up);

        // --- Phase 3: 물리 계산 (핵심) ---
        CalculatePhysics(t);
    }

    void CalculatePhysics(float t)
    {
        // A. 곡률(Curvature) 계산
        // 현재 위치의 접선(Forward)과 조금 앞의 접선을 비교하여 휜 정도 계산
        Vector3 currentTan = trackSpline.EvaluateTangent(t);
        Vector3 nextTan = trackSpline.EvaluateTangent((t + 0.01f) % 1f); // 조금 앞

        // 두 벡터 사이의 각도 (라디안)
        float angle = Vector3.SignedAngle(currentTan, nextTan, Vector3.up) * Mathf.Deg2Rad;

        // 곡률 k = dθ / ds (각도변화량 / 이동거리)
        // 이동거리 ds = 전체길이 * 0.01
        float ds = trackLength * 0.01f;
        currentCurvature = angle / ds;

        // B. 횡가속도 (Lateral G) 계산
        // 공식: a = v^2 * k (원심력)
        // G단위 변환: / 9.81
        lateralG = (speed * speed * currentCurvature) / 9.81f;

        // C. 전후가속도 (Longitudinal G) 계산
        // 이번 프레임 속도 변화량 / 시간
        // (간단하게 Input 기반으로 추정하거나, 실제 speed 변화량 미분)
        if (Input.GetKey(KeyCode.W)) longitudinalG = acceleration / 9.81f;
        else if (Input.GetKey(KeyCode.S)) longitudinalG = -braking / 9.81f;
        else longitudinalG = 0;
    }
}