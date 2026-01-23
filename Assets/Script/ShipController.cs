using UnityEngine;

public class ShipController : MonoBehaviour, IMotionSource
{
    [Header("1. 주행 성능")]
    public float enginePower = 5000f;  // 앞으로 가는 힘
    public float turnPower = 3000f;    // 회전하는 힘

    // [삭제됨] 가짜 파도 설정 변수들은 이제 필요 없습니다. Suimono Inspector에서 조절하세요.

    // 내부 물리 변수
    private Rigidbody rb;
    private Vector3 lastVelocity;

    // G-Force 저장용
    private float surgeG, swayG, heaveG;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // [중요] Suimono 부력과 함께 쓰려면 무게중심을 잘 잡아야 합니다.
        // 배가 자꾸 뒤집어지면 y값을 낮추세요 (예: -1.0f)
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    void FixedUpdate()
    {
        HandleMovement();
        // SimulateBuoyancy(); <--- [삭제] 이제 이 일은 Suimono 컴포넌트가 대신 합니다!
        CalculateGForce();
    }

    void HandleMovement()
    {
        // 1. 전진/후진 (W/S)
        float moveInput = 0f;
        if (Input.GetKey(KeyCode.W)) moveInput = 1f;
        else if (Input.GetKey(KeyCode.S)) moveInput = -1f;

        // 배가 물에 떠 있을 때만 힘을 줘야 더 리얼하지만, 일단은 그냥 밉니다.
        rb.AddForce(transform.forward * moveInput * enginePower);

        // 2. 회전 (A/D)
        float turnInput = 0f;
        if (Input.GetKey(KeyCode.D)) turnInput = 1f;
        else if (Input.GetKey(KeyCode.A)) turnInput = -1f;

        rb.AddTorque(Vector3.up * turnInput * turnPower);
    }

    void CalculateGForce()
    {
        // Suimono가 파도를 태워서 배를 흔들어 놓으면, 
        // Rigidbody의 속도(velocity)가 미친듯이 변합니다.
        // 우리는 그걸 미분해서 G값만 뽑아내면 됩니다.

        // 1. 가속도(F=ma) 구하기
        Vector3 acceleration = (rb.velocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = rb.velocity;

        // 2. 로컬 좌표로 변환 (배 기준의 G값)
        Vector3 localAccel = transform.InverseTransformDirection(acceleration);

        // 3. G 단위 변환 및 부드러운 필터링 (파도가 너무 튀면 여기서 Lerp를 쓰기도 함)
        surgeG = localAccel.z / 9.81f;
        swayG = localAccel.x / 9.81f;
        heaveG = localAccel.y / 9.81f; // 파도 때문에 이 값이 계속 꿀렁거릴 겁니다!
    }

    // IMotionSource 구현
    public float GetSurgeG() { return surgeG; }
    public float GetSwayG() { return swayG; }
    public float GetHeaveG() { return heaveG; }
}