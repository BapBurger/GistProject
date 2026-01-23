using UnityEngine;

public class ShipPhysicsController : MonoBehaviour, IMotionSource
{
    private Rigidbody rb;
    private Vector3 lastVelocity;

    [Header("1. 주행 성능 설정")]
    public float moveSpeed = 3000f; // 배는 힘이 많이 필요함
    public float turnSpeed = 1500f;

    [Header("2. 미끄러짐 방지 (코너링 개선)")]
    [Range(0, 1)] public float sideFriction = 0.9f; // 1에 가까울수록 옆으로 안 밀림 (볼스터 정확도 향상)

    [Header("3. 실시간 물리 정보 (출력용)")]
    public float surgeG; // 앞뒤
    public float swayG;  // 좌우 (볼스터)
    public float heaveG; // 위아래 (파도)

    // 내부 변수 (자동차 코드와 동일한 스무딩 로직)
    private float smoothG_Lat = 0f;
    private float smoothG_Lon = 0f;
    private float smoothG_Heave = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 오뚜기 설정 (안정적인 G값을 위해 필수)
        rb.centerOfMass = new Vector3(0, -2.0f, 0);
        lastVelocity = rb.velocity;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        UpdateShipMovement();
        CalculatePhysicsBasedGForce();
    }

    void UpdateShipMovement()
    {
        float v = Input.GetAxis("Vertical");   // W, S
        float h = Input.GetAxis("Horizontal"); // A, D

        // 1. 힘 가하기
        rb.AddRelativeForce(Vector3.forward * v * moveSpeed);
        rb.AddTorque(Vector3.up * h * turnSpeed);

        // 2. [핵심] 옆으로 미끄러짐(Drift) 억제 -> 볼스터가 정확하게 반응하도록 도움
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        localVel.x *= (1f - sideFriction); // 옆으로 가는 힘을 죽임
        rb.velocity = transform.TransformDirection(localVel);
    }

    void CalculatePhysicsBasedGForce()
    {
        float deltaTime = Time.fixedDeltaTime;
        if (deltaTime <= 0) return;

        // 1. 실제 가속도 계산 (속도 변화량 / 시간)
        Vector3 acceleration = (rb.velocity - lastVelocity) / deltaTime;

        // 2. 로컬 좌표로 변환 (배 기준의 앞/옆/위)
        Vector3 localAccel = transform.InverseTransformDirection(acceleration);

        // 3. Raw G값 추출 (배는 차보다 느리므로 G값을 좀 더 증폭시킴 * 2f ~ 5f)
        // 볼스터가 너무 약하면 아래의 2f, 5f 숫자를 키우세요.
        float rawLongitudinalG = (localAccel.z / 9.81f) * 2f;
        float rawLateralG = (localAccel.x / 9.81f) * 5f; // 횡가속도 증폭 (볼스터용)
        float rawHeaveG = (localAccel.y / 9.81f) * 5f; // 파도 느낌 증폭

        // 4. [자동차 코드와 동일] 부드러운 보간 (Lerp) 적용
        // 이 부분이 있어야 볼스터가 덜덜거리지 않고 부드럽게 움직임
        smoothG_Lon = Mathf.Lerp(smoothG_Lon, rawLongitudinalG, deltaTime * 3f);
        smoothG_Lat = Mathf.Lerp(smoothG_Lat, rawLateralG, deltaTime * 3f);
        smoothG_Heave = Mathf.Lerp(smoothG_Heave, rawHeaveG, deltaTime * 3f);

        // 5. 너무 작은 값은 0으로 처리 (떨림 방지)
        if (Mathf.Abs(smoothG_Lon) < 0.01f) smoothG_Lon = 0f;
        if (Mathf.Abs(smoothG_Lat) < 0.01f) smoothG_Lat = 0f;
        // Heave는 파도니까 0으로 자르지 않음

        // 최종 값 할당
        surgeG = smoothG_Lon;
        swayG = smoothG_Lat;
        heaveG = smoothG_Heave;

        lastVelocity = rb.velocity;
    }

    // 인터페이스 구현
    public float GetSurgeG() { return surgeG; }
    public float GetSwayG() { return swayG; }
    public float GetHeaveG() { return heaveG; } // 파도 값 리턴
}