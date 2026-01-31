using UnityEngine;
using UnityEngine.Splines;
using System.Collections;

/// <summary>
/// Spline 위를 따라 움직이는 차량 컨트롤러.
/// - AutoDrive: 커브(곡률) 기반 목표 속도 계산
/// - Station Stop: 종점 감속 -> 정차 -> 재출발
/// - Longitudinal G 진동 방지:
///     1) targetSpeed SmoothDamp(저역통과)
///     2) currentSpeed MoveTowards(단조롭게 목표 추종)
///     3) longitudinalG = "이번 프레임 실제 적용 가속(appliedAccel)" 기반
/// </summary>
public class CarPhysicsController : MonoBehaviour, IMotionSource
{
    [Header("1. 트랙 연결")]
    public SplineContainer splineContainer;
    public float heightOffset = 0.0f;

    [Header("2. AI 주행 설정")]
    public bool isAutoDrive = true;
    public float lookAheadDist = 10f;
    [Range(0.1f, 5f)] public float curveSensitivity = 2.0f;

    [Header("3. 정차 시나리오 설정")]
    public bool enableStationStop = true;
    public float slowDownDistance = 20f;
    public float stopDuration = 1.0f;
    public float stopTriggerDistance = 1.0f;   // 이 거리 이내면 정차 코루틴 진입 가능
    public float stopSpeedThreshold = 0.5f;    // 이 속도 이하면 '멈춘 것으로' 판단

    [Header("4. 주행 성능 (가속/감속/마찰)")]
    public float maxSpeed = 30f;
    public float corneringSpeed = 10f;
    public float acceleration = 10f; // m/s^2 (개념상)
    public float braking = 15f;      // m/s^2 (개념상)
    public float friction = 2f;      // manual에서 엑셀 안 밟을 때 감속

    [Header("5. 목표속도/가속도 스무딩 (진동 방지 핵심)")]
    [Tooltip("목표 속도(targetSpeed) 스무딩 시간. 0.2~0.5 권장")]
    public float targetSpeedSmoothTime = 0.25f;

    [Tooltip("G 값 스무딩 강도(클수록 더 빨리 따라감). 4~10 권장")]
    public float gSmoothing = 6f;

    [Header("6. 실시간 정보 (모니터링)")]
    public float currentSpeed = 0f;
    public float currentDistance = 0f;
    public float lateralG = 0f;
    public float longitudinalG = 0f;
    public string status = "Driving";

    // 내부 변수
    private float trackLength;

    // targetSpeed smoothing
    private float smoothTargetSpeed = 0f;
    private float targetSpeedVel = 0f;

    // applied accel (이번 프레임 실제 적용된 가속도)
    private float appliedAccel = 0f;

    // G smoothing
    private float smoothG_Lat = 0f;
    private float smoothG_Lon = 0f;

    // station stop state
    private bool isWaiting = false;
    private Coroutine stopCoroutine;

    void Start()
    {
        if (splineContainer != null)
        {
            trackLength = splineContainer.CalculateLength();
        }

        // 시작 시 목표 속도 초기화 (갑작스런 튐 방지)
        smoothTargetSpeed = currentSpeed;
    }

    void Update()
    {
        if (splineContainer == null) return;
        if (trackLength <= 0f) trackLength = splineContainer.CalculateLength();
        if (trackLength <= 0f) return;

        if (isWaiting)
        {
            // 정차 중에는 속도 0 고정
            currentSpeed = 0f;
            appliedAccel = 0f;
            status = "Waiting (Resting)";
        }
        else
        {
            if (isAutoDrive) UpdateAutoPilot();
            else UpdateManualDrive();
        }

        UpdatePosition();
        CalculatePhysicsBasedGForce();
    }

    void UpdateAutoPilot()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 남은 거리(종점까지)
        float distRemaining = Mathf.Max(0f, trackLength - currentDistance);

        // ---------------------------
        // 1) 목표 속도 계산
        // ---------------------------
        float targetSpeed = maxSpeed;

        // (A) 종점 감속 시나리오
        if (enableStationStop && distRemaining < slowDownDistance)
        {
            status = "Arriving (Braking)";

            float stopFactor = Mathf.Clamp01(distRemaining / Mathf.Max(0.0001f, slowDownDistance));
            // distRemaining=slowDownDistance -> maxSpeed, distRemaining=0 -> 0
            targetSpeed = Mathf.Lerp(0f, maxSpeed, stopFactor);
        }
        // (B) 일반 주행: 곡률 기반 목표 속도
        else
        {
            status = "Driving";

            float nextDist = currentDistance + lookAheadDist;
            if (nextDist > trackLength) nextDist -= trackLength;

            float t0 = currentDistance / trackLength;
            float t1 = nextDist / trackLength;

            Vector3 dir0 = splineContainer.EvaluateTangent(t0);
            Vector3 dir1 = splineContainer.EvaluateTangent(t1);

            // tangent가 0이 될 가능성 대비
            if (dir0.sqrMagnitude < 1e-6f || dir1.sqrMagnitude < 1e-6f)
            {
                targetSpeed = maxSpeed;
            }
            else
            {
                float angle = Vector3.Angle(dir0, dir1); // 0~180
                float curvatureFactor = Mathf.Clamp01(angle * curveSensitivity * 0.1f);
                targetSpeed = Mathf.Lerp(maxSpeed, corneringSpeed, curvatureFactor);
            }
        }

        targetSpeed = Mathf.Clamp(targetSpeed, 0f, maxSpeed);

        // ---------------------------
        // 2) 목표 속도 스무딩 (저역통과)
        // ---------------------------
        smoothTargetSpeed = Mathf.SmoothDamp(
            smoothTargetSpeed,
            targetSpeed,
            ref targetSpeedVel,
            Mathf.Max(0.0001f, targetSpeedSmoothTime)
        );

        // ---------------------------
        // 3) 현재 속도는 MoveTowards로 단조 추종 (토글 제거)
        // ---------------------------
        float rate = (currentSpeed > smoothTargetSpeed) ? braking : acceleration;

        float prevSpeed = currentSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, smoothTargetSpeed, rate * dt);
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        // 이번 프레임 적용 가속도(=G 계산에 사용)
        appliedAccel = (currentSpeed - prevSpeed) / dt;

        // ---------------------------
        // 4) 완전 정차 트리거
        // ---------------------------
        if (enableStationStop)
        {
            float distRemainingNow = Mathf.Max(0f, trackLength - currentDistance);

            // 종점 아주 근처 & 속도 충분히 낮으면 정차 코루틴 진입
            if (distRemainingNow <= stopTriggerDistance && currentSpeed <= stopSpeedThreshold)
            {
                if (stopCoroutine == null)
                    stopCoroutine = StartCoroutine(StationStopRoutine());
            }
        }
    }

    IEnumerator StationStopRoutine()
    {
        isWaiting = true;

        // 종점에 고정 (원하면 0이 아니라 trackLength로 두고, position 업데이트에서 clamp해도 됨)
        currentSpeed = 0f;
        appliedAccel = 0f;

        // 종점에서 정차: 현재 거리 = trackLength로 고정 (더 자연스러움)
        // 그리고 UpdatePosition에서 t=1에 대응하도록 처리
        currentDistance = trackLength;

        Debug.Log("도착! 정차합니다...");
        yield return new WaitForSeconds(stopDuration);

        Debug.Log("다시 출발!");
        // 재출발: 트랙 처음으로 이동
        currentDistance = 0f;

        // 스무딩 상태도 초기화(갑작 튐 방지)
        smoothTargetSpeed = 0f;
        targetSpeedVel = 0f;

        isWaiting = false;
        stopCoroutine = null;
    }

    void UpdateManualDrive()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float prevSpeed = currentSpeed;
        float accelCmd = 0f;

        if (Input.GetKey(KeyCode.W)) accelCmd = acceleration;
        else if (Input.GetKey(KeyCode.S)) accelCmd = -braking;
        else
        {
            // 엑셀을 떼면 마찰로 서서히 감속
            if (currentSpeed > 0.1f) accelCmd = -friction;
            else if (currentSpeed < -0.1f) accelCmd = friction;
            else { currentSpeed = 0f; accelCmd = 0f; }
        }

        currentSpeed += accelCmd * dt;
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        appliedAccel = (currentSpeed - prevSpeed) / dt;

        // 수동에서도 stop 시나리오를 쓰고 싶으면 여기에도 distRemaining 조건을 넣으면 됨
        status = "Manual";
    }

    void UpdatePosition()
    {
        if (trackLength <= 0f) return;

        // 이동
        currentDistance += currentSpeed * Time.deltaTime;

        // 종점 정차 시나리오가 꺼져있으면 루프
        if (!enableStationStop)
        {
            if (currentDistance > trackLength) currentDistance -= trackLength;
        }
        else
        {
            // enableStationStop일 때는 종점 넘어가면 clamp
            currentDistance = Mathf.Clamp(currentDistance, 0f, trackLength);
        }

        float t = (trackLength > 0f) ? (currentDistance / trackLength) : 0f;
        t = Mathf.Clamp01(t);

        Vector3 worldPos = splineContainer.EvaluatePosition(t);
        Vector3 worldDir = splineContainer.EvaluateTangent(t);
        Vector3 worldUp = splineContainer.EvaluateUpVector(t);

        transform.position = worldPos + (Vector3.up * heightOffset);

        if (worldDir.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(worldDir, worldUp);
    }

    void CalculatePhysicsBasedGForce()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Longitudinal G: "이번 프레임 실제 적용 가속도" 기반
        float rawLongitudinalG = appliedAccel / 9.81f;

        // Lateral G: tangent 변화(곡률) 기반의 간단 모델 (기존 유지)
        // 노이즈가 크면 lookAheadForG를 1~3m로 키우거나, angleDiff를 필터링하면 안정됨.
        float lookAheadForG = 1.0f;
        float nextDist = currentDistance + lookAheadForG;
        if (nextDist > trackLength) nextDist = trackLength;

        float t0 = currentDistance / trackLength;
        float t1 = nextDist / trackLength;

        Vector3 tan0 = splineContainer.EvaluateTangent(t0);
        Vector3 tan1 = splineContainer.EvaluateTangent(t1);

        float angleDiff = 0f;
        if (tan0.sqrMagnitude > 1e-6f && tan1.sqrMagnitude > 1e-6f)
            angleDiff = Vector3.SignedAngle(tan0, tan1, Vector3.up);

        // 기존의 경험적 스케일 유지
        float rawLateralG = (angleDiff * currentSpeed * currentSpeed) * 0.005f;

        // G 스무딩
        float lerpK = Mathf.Clamp(gSmoothing * dt, 0f, 1f);
        smoothG_Lon = Mathf.Lerp(smoothG_Lon, rawLongitudinalG, lerpK);
        smoothG_Lat = Mathf.Lerp(smoothG_Lat, rawLateralG, lerpK);

        longitudinalG = smoothG_Lon;
        lateralG = smoothG_Lat;
    }

    // IMotionSource
    public float GetSurgeG() { return longitudinalG; }
    public float GetSwayG() { return lateralG; }
    public float GetHeaveG() { return 0f; }
}
