using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CarPhysicsController : MonoBehaviour, IMotionSource
{
    [Header("1. 트랙 연결")]
    public SplineContainer splineContainer;

    [Header("2. 주행 성능 설정")]
    public float maxSpeed = 30f;
    public float acceleration = 10f;
    public float braking = 20f;
    public float friction = 2f;

    [Header("3. 실시간 물리 정보 (출력용)")]
    public float currentSpeed = 0f;
    public float lateralG = 0f;        // 횡가속도 (좌우)
    public float longitudinalG = 0f;   // 전후가속도 (앞뒤)


    // 내부 변수
    private float currentDistance = 0f;
    private float trackLength;
    private float smoothG_Lat = 0f;
    private float smoothG_Lon = 0f;
    private float lastFrameSpeed = 0f; // 실제 가속도 계산용

    void Start()
    {
        if (splineContainer != null)
            trackLength = splineContainer.CalculateLength();
    }

    void Update()
    {
        if (splineContainer == null) return;

        UpdateCarMovement();
        CalculatePhysicsBasedGForce();
    }

    void UpdateCarMovement()
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

        if (trackLength > 0)
        {
            currentDistance += currentSpeed * Time.deltaTime;
            if (currentDistance > trackLength) currentDistance -= trackLength;

            float t = currentDistance / trackLength;
            Vector3 localPos = splineContainer.EvaluatePosition(t);
            Vector3 localDir = splineContainer.EvaluateTangent(t);
            Vector3 localUp = splineContainer.EvaluateUpVector(t);

            transform.position = splineContainer.transform.TransformPoint(localPos);

            Vector3 worldDir = splineContainer.transform.TransformDirection(localDir);
            Vector3 worldUp = splineContainer.transform.TransformDirection(localUp);

            if (worldDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(worldDir, worldUp);
        }
    }

    void CalculatePhysicsBasedGForce()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0) return;

        // [핵심] 실제 속도 변화량으로 가속도 계산 (정지 시 0 보장)
        float actualAcceleration = (currentSpeed - lastFrameSpeed) / deltaTime;
        lastFrameSpeed = currentSpeed;

        float rawLongitudinalG = actualAcceleration / 9.81f;

        // 횡가속도 계산
        float lookAheadDist = currentDistance + 1.0f;
        if (lookAheadDist > trackLength) lookAheadDist -= trackLength;
        float nextT = lookAheadDist / trackLength;

        Vector3 currentTan = splineContainer.EvaluateTangent(currentDistance / trackLength);
        Vector3 nextTan = splineContainer.EvaluateTangent(nextT);
        float angleDiff = Vector3.SignedAngle(currentTan, nextTan, Vector3.up);

        float rawLateralG = (angleDiff * currentSpeed * currentSpeed) * 0.005f;

        smoothG_Lon = Mathf.Lerp(smoothG_Lon, rawLongitudinalG, deltaTime * 5f);
        smoothG_Lat = Mathf.Lerp(smoothG_Lat, rawLateralG, deltaTime * 5f);

        if (Mathf.Abs(smoothG_Lon) < 0.01f) smoothG_Lon = 0f;
        if (Mathf.Abs(smoothG_Lat) < 0.01f) smoothG_Lat = 0f;

        longitudinalG = smoothG_Lon;
        lateralG = smoothG_Lat;
    }

    public float GetSurgeG()
    {
        return longitudinalG; // 기존에 계산해둔 변수 리턴
    }

    public float GetSwayG()
    {
        return lateralG;
    }

    public float GetHeaveG()
    {
        return 0f; // 자동차는 평지니까 일단 0
    }
}