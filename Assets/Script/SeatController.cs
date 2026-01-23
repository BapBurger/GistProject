using UnityEngine;

public class SeatController : MonoBehaviour
{
    // [Header("1. 자동차 연결")]
    // public CarPhysicsController connectedCar;
    
    
    // [중요] 이제 특정 차가 아니라, '인터페이스'를 담습니다.
    private IMotionSource currentSource;

    [Header("2. 물리 기반 튜닝 (Realism)")]
    [Tooltip("반응 속도: 너무 빠르면 '회전'으로 느껴짐 (추천: 2 ~ 5)")]
    public float tiltSpeed = 2.0f;

    [Tooltip("최대 틸트 각도 제한 (하드웨어 보호용, 단위: 도)")]
    public float maxPitchLimit = 20.0f;

    [Header("3. 코너링 및 슬라이드 게인")]
    public float slideGain = 0.5f;     // 앞뒤 슬라이드 민감도
    public float bolsterGain = 30.0f;  // 볼스터 민감도

    [Header("4. 부품 인덱스 설정")]
    public int wholeSlideIndex = 0;
    public int backSeatIndex = 1;
    public int rightBolsterIndex = 2;
    public int leftBolsterIndex = 3;
    public int rightBackBolsterIndex = 4;
    public int leftBackBolsterIndex = 5;

    // ▼▼▼ [추가] 전체 시트 위아래(Lift) 인덱스
    public int wholeLiftIndex = 6;
    public float heaveGain = 50.0f;


    [Header("5. 시트 부품 상세 설정")]
    public SeatPart[] seatParts;

    // 외부(매니저)에서 연결 대상을 바꿔주는 함수
    public void ConnectVehicle(IMotionSource newVehicle)
    {
        currentSource = newVehicle;
        Debug.Log($"[SeatController] 새로운 소스가 연결됨: {newVehicle}");
    }

    void Start()
    {
        foreach (var part in seatParts) part.Initialize();
    }

    void Update()
    {
        // 연결된 소스가 없으면 아무것도 안 함
        if (currentSource == null) return;

        ProcessSimulation();
    }

    void ProcessSimulation()
    {
        float surgeG = currentSource.GetSurgeG();
        float swayG = currentSource.GetSwayG();

        // ------------------------------------------------------------------
        // [1] 물리 기반 틸트 (Tilt Coordination)
        // 공식: Theta = Asin( G_force )
        // ------------------------------------------------------------------

        // 1. G값을 -1 ~ 1 사이로 안전하게 자름 (Asin 함수 에러 방지)
        float clampedG = Mathf.Clamp(surgeG, -1.0f, 1.0f);

        // 2. 아크사인으로 정확한 각도 계산 (Radian -> Degree)
        float targetAngle = Mathf.Asin(clampedG) * Mathf.Rad2Deg;

        // 3. 하드웨어 한계 보호
        targetAngle = Mathf.Clamp(targetAngle, -maxPitchLimit, maxPitchLimit);

        // 4. 등받이 적용 (가속 시 뒤로 눕힘, True = 물리 각도 모드)
        ApplyMotion(backSeatIndex, targetAngle, true);


        // ------------------------------------------------------------------
        // [2] 슬라이드 & 볼스터 (기존 로직 유지)
        // ------------------------------------------------------------------

        // 전체 슬라이드 (가속 시 뒤로 밀림)
        ApplyMotion(wholeSlideIndex, surgeG * slideGain, false);

        // 볼스터 계산
        float rightTarget = (swayG > 0) ? swayG * bolsterGain : 0;
        float leftTarget = (swayG < 0) ? swayG * bolsterGain : 0;

        // 엉덩이 볼스터 적용
        ApplyMotion(rightBolsterIndex, rightTarget, false);
        ApplyMotion(leftBolsterIndex, leftTarget, false);

        // 등쪽 볼스터 적용 (방향 반대이므로 마이너스 - 붙임)
        ApplyMotion(rightBackBolsterIndex, -rightTarget, false);
        ApplyMotion(leftBackBolsterIndex, -leftTarget, false);
    }

    // isPhysicsAngle: True면 Gain을 곱하지 않고 각도를 그대로 사용
    void ApplyMotion(int index, float targetValue, bool isPhysicsAngle)
    {
        if (index < 0 || index >= seatParts.Length) return;
        SeatPart part = seatParts[index];

        float finalTarget = targetValue;

        // 물리 모드가 아닐 때는(슬라이드, 볼스터) 초기값 기준 상대 이동
        if (!isPhysicsAngle)
        {
            finalTarget = part.initialValue + targetValue;
        }
        else
        {
            // 물리 모드(등받이)는 초기 각도에서 계산된 각도만큼 더함
            finalTarget = part.initialValue + targetValue;
        }

        // Min/Max 제한 적용
        finalTarget = Mathf.Clamp(finalTarget, part.minLimit, part.maxLimit);

        // [리얼함의 핵심] 부드러운 이동 (MoveTowards + Lerp 혼합)
        // 너무 빠르면 기계적이고, 너무 느리면 반응이 굼뜸 -> 적절한 tiltSpeed 찾기가 중요
        part.currentValue = Mathf.Lerp(part.currentValue, finalTarget, Time.deltaTime * tiltSpeed);

        UpdateTransform(part);
    }

    void UpdateTransform(SeatPart part)
    {
        if (part.targetTransform == null) return;

        if (part.moveType == MoveType.SlideZ)
        {
            Vector3 p = part.targetTransform.localPosition; p.z = part.currentValue; part.targetTransform.localPosition = p;
        }
        else if (part.moveType == MoveType.SlideY)
        {
            Vector3 p = part.targetTransform.localPosition; p.y = part.currentValue; part.targetTransform.localPosition = p;
        }
        else if (part.moveType == MoveType.RotateX)
        {
            part.targetTransform.localRotation = Quaternion.Euler(part.currentValue, part.fixedY, part.fixedZ);
        }
        else if (part.moveType == MoveType.RotateY)
        {
            part.targetTransform.localRotation = Quaternion.Euler(part.fixedX, part.currentValue, part.fixedZ);
        }
    }
}


[System.Serializable]
public class SeatPart
{
    public string partName;
    public Transform targetTransform;
    public MoveType moveType;
    public float minLimit = -50f;
    public float maxLimit = 50f;

    [Header("Debug Info")]
    public float currentValue;

    [HideInInspector] public float initialValue;
    [HideInInspector] public float fixedX;
    [HideInInspector] public float fixedY;
    [HideInInspector] public float fixedZ;

    public void Initialize()
    {
        if (targetTransform == null) return;
        fixedX = targetTransform.localEulerAngles.x;
        fixedY = targetTransform.localEulerAngles.y;
        fixedZ = targetTransform.localEulerAngles.z;

        if (moveType == MoveType.SlideZ) currentValue = targetTransform.localPosition.z;
        else if (moveType == MoveType.SlideY) currentValue = targetTransform.localPosition.y;
        else if (moveType == MoveType.RotateX)
        {
            currentValue = targetTransform.localEulerAngles.x;
            if (currentValue > 180) currentValue -= 360;
        }
        else if (moveType == MoveType.RotateY)
        {
            currentValue = targetTransform.localEulerAngles.y;
            if (currentValue > 180) currentValue -= 360;
        }
        initialValue = currentValue;
    }
}

public enum MoveType { RotateX, SlideZ, RotateY, SlideY }