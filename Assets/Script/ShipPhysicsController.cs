using UnityEngine;

public class ShipPhysicsController : MonoBehaviour, IMotionSource
{
    private Rigidbody rb;
    private Vector3 lastVelocity;

    [Header("1. 주행 성능 설정")]
    public float moveSpeed = 5000f; // 마찰이 줄면 속도가 빨라지니 적당히 조절 필요
    public float turnSpeed = 2000f;

    [Header("2. 스마트 물 마찰력 (핵심)")]
    [Tooltip("앞뒤 저항: 낮을수록 배가 쭉 미끄러짐 (관성 주행)")]
    [Range(0, 5)] public float forwardDrag = 0.5f; // 0.5 추천 (관성 좋음)

    [Tooltip("좌우 저항: 높을수록 드리프트가 줄어듦")]
    [Range(0, 5)] public float sideDrag = 2.0f;    // 2.0 추천 (옆으로 밀림 방지)

    [Tooltip("상하 저항: 높을수록 튀어오름(로켓) 방지")]
    [Range(0, 10)] public float verticalDrag = 10.0f; // 10.0 필수 (부력 억제용)

    [Header("3. 실시간 물리 정보 (출력용)")]
    public float surgeG;
    public float swayG;
    public float heaveG;

    [Header("4. Sway 튜닝")]
    [Range(0, 10)] public float motionSwayGain = 5.0f;
    [Range(0, 10)] public float gravitySwayGain = 3.0f;

    private float smoothG_Lat = 0f;
    private float smoothG_Lon = 0f;
    private float smoothG_Heave = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -2.0f, 0);
        lastVelocity = rb.velocity;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        UpdateShipMovement();
        ApplyCustomWaterDrag(); // [추가됨] 방향별 마찰력 적용
        CalculatePhysicsBasedGForce();
    }

    void UpdateShipMovement()
    {
        float v = Input.GetAxis("Vertical");
        float h = Input.GetAxis("Horizontal");

        // 힘 가하기
        rb.AddRelativeForce(Vector3.forward * v * moveSpeed);
        rb.AddTorque(Vector3.up * h * turnSpeed);
    }

    // [핵심 로직] 방향마다 다른 마찰력을 적용하는 함수
    void ApplyCustomWaterDrag()
    {
        // 1. 배 기준의 로컬 속도로 변환
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);

        // 2. 축별로 다른 감속(Drag) 적용
        // Time.fixedDeltaTime을 곱해서 프레임 드랍에도 일정하게 작동

        // Z축(앞뒤): 관성을 살리기 위해 저항을 아주 조금만 줌
        localVel.z *= (1f - (forwardDrag * Time.fixedDeltaTime));

        // X축(좌우): 미끄러짐 방지를 위해 적당한 저항
        localVel.x *= (1f - (sideDrag * Time.fixedDeltaTime));

        // Y축(상하): 튀어 오름 방지를 위해 아주 강력한 저항 (기존 Drag 10 효과)
        localVel.y *= (1f - (verticalDrag * Time.fixedDeltaTime));

        // 3. 다시 월드 속도로 변환하여 적용
        rb.velocity = transform.TransformDirection(localVel);
    }

    void CalculatePhysicsBasedGForce()
    {
        float deltaTime = Time.fixedDeltaTime;
        if (deltaTime <= 0) return;

        Vector3 acceleration = (rb.velocity - lastVelocity) / deltaTime;
        Vector3 localAccel = transform.InverseTransformDirection(acceleration);

        // Sway 계산 (원심력 + 기울기)
        float rawMotionSway = (localAccel.x / 9.81f);
        float rollAngle = transform.localEulerAngles.z;
        if (rollAngle > 180) rollAngle -= 360;
        float rawGravitySway = Mathf.Sin(rollAngle * Mathf.Deg2Rad);
        float finalSwayG = (rawMotionSway * motionSwayGain) + (rawGravitySway * gravitySwayGain);

        float rawSurgeG = (localAccel.z / 9.81f) * 2f;
        float rawHeaveG = (localAccel.y / 9.81f) * 5f;

        // 스무딩
        smoothG_Lon = Mathf.Lerp(smoothG_Lon, rawSurgeG, deltaTime * 3f);
        smoothG_Lat = Mathf.Lerp(smoothG_Lat, finalSwayG, deltaTime * 3f);
        smoothG_Heave = Mathf.Lerp(smoothG_Heave, rawHeaveG, deltaTime * 3f);

        if (Mathf.Abs(smoothG_Lon) < 0.01f) smoothG_Lon = 0f;
        if (Mathf.Abs(smoothG_Lat) < 0.01f) smoothG_Lat = 0f;

        surgeG = smoothG_Lon;
        swayG = smoothG_Lat;
        heaveG = smoothG_Heave;

        lastVelocity = rb.velocity;
    }

    public float GetSurgeG() { return surgeG; }
    public float GetSwayG() { return swayG; }
    public float GetHeaveG() { return heaveG; }
}