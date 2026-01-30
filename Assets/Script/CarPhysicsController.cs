using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CarPhysicsController : MonoBehaviour, IMotionSource
{
    [Header("1. 트랙 연결")]
    public SplineContainer splineContainer;

    [Header("2. 높이 조절")]
    public float heightOffset = 0.0f; // 이 값을 조절해서 차를 내릴 겁니다.

    [Header("2. AI 주행 설정 (자율주행)")]
    public bool isAutoDrive = true;   // 체크하면 AI가 운전함
    public float lookAheadDist = 10f; // 몇 미터 앞을 미리 볼 것인가? (코너 감지용)
    [Range(0.1f, 5f)]
    public float curveSensitivity = 2.0f; // 값이 클수록 코너에서 겁을 먹고 속도를 많이 줄임

    [Header("3. 주행 성능 설정")]
    public float maxSpeed = 30f;      // 직선 최고 속도
    public float corneringSpeed = 10f; // 코너 최저 속도 (급커브일 때 목표 속도)
    public float acceleration = 10f;  // 가속력 (Surge +)
    public float braking = 15f;       // 제동력 (Surge -)
    public float friction = 2f;       // 자연 감속

    [Header("4. 실시간 물리 정보 (출력용)")]
    public float currentSpeed = 0f;
    public float lateralG = 0f;        // 횡가속도 (Sway)
    public float longitudinalG = 0f;   // 전후가속도 (Surge)

    // 내부 변수
    private float currentDistance = 0f;
    private float trackLength;
    private float smoothG_Lat = 0f;
    private float smoothG_Lon = 0f;
    private float lastFrameSpeed = 0f;


    void Start()
    {
        if (splineContainer != null)
            trackLength = splineContainer.CalculateLength();
    }

    void Update()
    {
        if (splineContainer == null) return;

        if (isAutoDrive)
        {
            UpdateAutoPilot(); // AI 운전
        }
        else
        {
            UpdateManualDrive(); // 기존 W/S 운전
        }

        UpdatePosition(); // 위치 이동
        CalculatePhysicsBasedGForce(); // G값 계산
    }

    // [핵심] AI 자율주행 로직
    void UpdateAutoPilot()
    {
        // 1. 앞을 미리 내다봄 (Look Ahead)
        float nextDist = currentDistance + lookAheadDist;
        if (nextDist > trackLength) nextDist -= trackLength; // 트랙 한바퀴 돌았을 때 처리

        float tCurrent = currentDistance / trackLength;
        float tNext = nextDist / trackLength;

        // 2. 현재 방향 vs 미래 방향 비교 (코너가 얼마나 심한가?)
        Vector3 currentDir = splineContainer.EvaluateTangent(tCurrent);
        Vector3 nextDir = splineContainer.EvaluateTangent(tNext);

        // 두 벡터 사이의 각도 (0도면 직선, 90도면 직각 코너)
        float angle = Vector3.Angle(currentDir, nextDir);

        // 3. 목표 속도 계산
        // 각도가 클수록(급커브) -> corneringSpeed에 가까워짐
        // 각도가 작을수록(직선) -> maxSpeed에 가까워짐
        // curveSensitivity로 민감도 조절
        float curvatureFactor = Mathf.Clamp01(angle * curveSensitivity * 0.1f);
        float targetSpeed = Mathf.Lerp(maxSpeed, corneringSpeed, curvatureFactor);

        // 4. 가속 또는 감속 결정
        if (currentSpeed < targetSpeed)
        {
            // 목표 속도보다 느리면 가속 (엑셀)
            currentSpeed += acceleration * Time.deltaTime;
        }
        else
        {
            // 목표 속도보다 빠르면 감속 (브레이크)
            // 코너 앞에서는 더 강하게 제동
            currentSpeed -= braking * Time.deltaTime;
        }

        // 속도 제한
        currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);
    }

    void UpdateManualDrive()
    {
        float targetAccel = 0f;
        if (Input.GetKey(KeyCode.W)) targetAccel = acceleration;
        else if (Input.GetKey(KeyCode.S)) targetAccel = -braking;
        else
        {
            if (currentSpeed > 0.1f) targetAccel = -friction;
            else if (currentSpeed < -0.1f) targetAccel = friction;
            else { currentSpeed = 0f; targetAccel = 0f; }
        }
        currentSpeed += targetAccel * Time.deltaTime;
        currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);
    }

    void UpdatePosition()
    {
        // 안전장치
        if (trackLength <= 0 && splineContainer != null)
            trackLength = splineContainer.CalculateLength();

        if (trackLength > 0)
        {
            // 1. 거리 계산
            currentDistance += currentSpeed * Time.deltaTime;

            // 뺑뺑이 (Loop) 처리
            if (currentDistance > trackLength) currentDistance -= trackLength;
            if (currentDistance < 0) currentDistance += trackLength;

            float t = currentDistance / trackLength;

            // 2. [핵심 수정] TransformPoint를 쓰지 않고 바로 받아옵니다.
            // SplineContainer.EvaluatePosition은 기본적으로 '월드 좌표'를 줍니다.
            Vector3 worldPos = splineContainer.EvaluatePosition(t);
            Vector3 worldDir = splineContainer.EvaluateTangent(t);
            Vector3 worldUp = splineContainer.EvaluateUpVector(t);

            // 3. 위치 적용 (여기서 아까 말한 높이 조절도 같이 적용)
            // TransformPoint를 지웠습니다!
            transform.position = worldPos + (Vector3.up * heightOffset);

            // 4. 회전 적용
            if (worldDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(worldDir, worldUp);
        }
    }

    void CalculatePhysicsBasedGForce()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0) return;

        // 실제 속도 변화량으로 Surge G 계산
        float actualAcceleration = (currentSpeed - lastFrameSpeed) / deltaTime;
        lastFrameSpeed = currentSpeed;
        float rawLongitudinalG = actualAcceleration / 9.81f;

        // 코너링으로 인한 Sway G 계산 (원심력)
        float lookAheadDistForG = 1.0f; // G값 계산용 짧은 앞보기
        float nextDist = currentDistance + lookAheadDistForG;
        if (nextDist > trackLength) nextDist -= trackLength;

        Vector3 currentTan = splineContainer.EvaluateTangent(currentDistance / trackLength);
        Vector3 nextTan = splineContainer.EvaluateTangent(nextDist / trackLength);
        float angleDiff = Vector3.SignedAngle(currentTan, nextTan, Vector3.up); // 좌우 구분을 위해 SignedAngle 사용

        // 공식: F = mv^2/r (여기서는 근사치로 각도변화 * 속도제곱 사용)
        float rawLateralG = (angleDiff * currentSpeed * currentSpeed) * 0.005f;

        // 부드럽게 필터링
        smoothG_Lon = Mathf.Lerp(smoothG_Lon, rawLongitudinalG, deltaTime * 5f);
        smoothG_Lat = Mathf.Lerp(smoothG_Lat, rawLateralG, deltaTime * 5f);

        longitudinalG = smoothG_Lon;
        lateralG = smoothG_Lat;
    }

    public float GetSurgeG() { return longitudinalG; }
    public float GetSwayG() { return lateralG; }
    public float GetHeaveG() { return 0f; }
}