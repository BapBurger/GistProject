using UnityEngine;

public class SeatController : MonoBehaviour
{
    private IMotionSource currentSource;

    [Header("1. 모션 필터 (Washout & Deadzone)")]
    [Tooltip("워시아웃: 값이 클수록 시트가 빨리 제자리로 돌아옵니다. (0이면 안 돌아옴, 추천: 0.5 ~ 2.0)")]
    public float washoutRate = 1.0f;

    [Tooltip("데드존: 이 값보다 작은 미세한 떨림은 무시합니다. (추천: 0.05)")]
    public float deadZone = 0.05f;

    [Header("2. 물리 기반 튜닝 (반응성)")]
    [Tooltip("반응 속도 (추천: 2 ~ 5)")]
    public float tiltSpeed = 3.0f;
    [Tooltip("최대 틸트 각도 제한")]
    public float maxTiltLimit = 20.0f;

    [Header("3. 모션 게인 (강도 조절)")]
    public float slideGain = 0.5f;
    public float heaveGain = 0.1f;     // [수정] 기본값을 0.5 -> 0.1로 대폭 낮췄습니다.
    public float bolsterGain = 30.0f;

    [Header("4. 부품 인덱스")]
    public int wholeSlideIndex = 0;
    public int backSeatIndex = 1;
    public int rightBolsterIndex = 2;
    public int leftBolsterIndex = 3;
    public int rightBackBolsterIndex = 4;
    public int leftBackBolsterIndex = 5;
    public int wholeLiftIndex = 6;

    [Header("5. 시트 부품 설정")]
    public SeatPart[] seatParts;

    // 내부 계산용 변수 (워시아웃 필터링용)
    private float filteredSurge = 0f;
    private float filteredSway = 0f;
    private float filteredHeave = 0f;

    public void ConnectVehicle(IMotionSource newVehicle)
    {
        currentSource = newVehicle;
        // 차량이 바뀌면 필터값 초기화
        filteredSurge = 0f;
        filteredSway = 0f;
        filteredHeave = 0f;
    }

    void Start()
    {
        foreach (var part in seatParts) part.Initialize();
    }

    void Update()
    {
        if (currentSource == null) return;
        ProcessSimulation();
    }

    void ProcessSimulation()
    {
        // 1. 원본 데이터 가져오기
        float rawSurge = currentSource.GetSurgeG();
        float rawSway = currentSource.GetSwayG();
        float rawHeave = currentSource.GetHeaveG();

        // 2. 데드존 처리 (너무 작은 값은 0으로)
        if (Mathf.Abs(rawSurge) < deadZone) rawSurge = 0;
        if (Mathf.Abs(rawSway) < deadZone) rawSway = 0;
        if (Mathf.Abs(rawHeave) < deadZone) rawHeave = 0;

        // 3. 워시아웃 필터 적용 (핵심!)
        // 목표값(raw)을 향해 가되, 입력이 멈추면 washoutRate 속도로 0으로 돌아가려는 성질
        // (입력값 - 현재값)을 더해주면서, 동시에 0쪽으로 서서히 값을 깎아냄

        // 간단한 구현: 입력을 부드럽게 따라가되, 지속적인 입력은 감쇠시킴 (High-pass filter 유사 효과)
        // 여기서는 직관적인 "Leaky Integrator" 방식을 씁니다.

        // (1) 일단 입력을 따라감
        filteredSurge = Mathf.Lerp(filteredSurge, rawSurge, Time.deltaTime * tiltSpeed);
        filteredSway = Mathf.Lerp(filteredSway, rawSway, Time.deltaTime * tiltSpeed);
        filteredHeave = Mathf.Lerp(filteredHeave, rawHeave, Time.deltaTime * tiltSpeed);

        // (2) 워시아웃: 매 프레임마다 0을 향해 조금씩 강제로 이동시킴 (Leaking)
        // 엑셀을 꾹 밟고 있어도(상수값 입력), 시간이 지나면 0이 됨.
        filteredSurge = Mathf.Lerp(filteredSurge, 0f, Time.deltaTime * washoutRate);
        filteredSway = Mathf.Lerp(filteredSway, 0f, Time.deltaTime * washoutRate);
        filteredHeave = Mathf.Lerp(filteredHeave, 0f, Time.deltaTime * washoutRate);


        // ------------------------------------------------------------------
        // [A] Pitch (X축 회전) - filteredSurge 사용
        // ------------------------------------------------------------------
        float clampedSurge = Mathf.Clamp(filteredSurge, -1.0f, 1.0f);
        float targetPitchAngle = Mathf.Asin(clampedSurge) * Mathf.Rad2Deg;
        targetPitchAngle = Mathf.Clamp(targetPitchAngle, -maxTiltLimit, maxTiltLimit);

        ApplyMotion(backSeatIndex, targetPitchAngle, true);

        // ------------------------------------------------------------------
        // [B] Slide & Lift - filtered 값 사용
        // ------------------------------------------------------------------
        ApplyMotion(wholeSlideIndex, filteredSurge * slideGain, false);
        ApplyMotion(wholeLiftIndex, filteredHeave * heaveGain, false); // Heave 적용

        // ------------------------------------------------------------------
        // [C] Bolster - filteredSway 사용
        // ------------------------------------------------------------------
        float rightTarget = (filteredSway > 0) ? filteredSway * bolsterGain : 0;
        float leftTarget = (filteredSway < 0) ? -filteredSway * bolsterGain : 0;

        ApplyMotion(rightBolsterIndex, rightTarget, false);
        ApplyMotion(leftBolsterIndex, leftTarget, false);
        ApplyMotion(rightBackBolsterIndex, -rightTarget, false);
        ApplyMotion(leftBackBolsterIndex, -leftTarget, false);
    }

    void ApplyMotion(int index, float targetValue, bool isPhysicsAngle)
    {
        if (index < 0 || index >= seatParts.Length) return;

        SeatPart part = seatParts[index];
        float finalTarget = targetValue;

        if (!isPhysicsAngle) finalTarget = part.initialValue + targetValue;
        else finalTarget = part.initialValue + targetValue;

        finalTarget = Mathf.Clamp(finalTarget, part.minLimit, part.maxLimit);

        // 필터링된 값을 사용하므로 여기서는 즉시 반응해도 부드러움
        part.currentValue = finalTarget;

        UpdateTransform(part);
    }

    void UpdateTransform(SeatPart part)
    {
        if (part.targetTransform == null) return;

        switch (part.moveType)
        {
            case MoveType.SlideZ:
                Vector3 pZ = part.targetTransform.localPosition; pZ.z = part.currentValue; part.targetTransform.localPosition = pZ;
                break;
            case MoveType.SlideY:
                Vector3 pY = part.targetTransform.localPosition; pY.y = part.currentValue; part.targetTransform.localPosition = pY;
                break;
            case MoveType.RotateX:
                part.targetTransform.localRotation = Quaternion.Euler(part.currentValue, part.fixedY, part.fixedZ);
                break;
            case MoveType.RotateY:
                part.targetTransform.localRotation = Quaternion.Euler(part.fixedX, part.currentValue, part.fixedZ);
                break;
            case MoveType.RotateZ:
                part.targetTransform.localRotation = Quaternion.Euler(part.fixedX, part.fixedY, part.currentValue);
                break;
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
        else if (moveType == MoveType.RotateX) currentValue = FixAngle(targetTransform.localEulerAngles.x);
        else if (moveType == MoveType.RotateY) currentValue = FixAngle(targetTransform.localEulerAngles.y);
        else if (moveType == MoveType.RotateZ) currentValue = FixAngle(targetTransform.localEulerAngles.z);

        initialValue = currentValue;
    }
    float FixAngle(float angle) { return angle > 180 ? angle - 360 : angle; }
}
public enum MoveType { RotateX, SlideZ, RotateY, SlideY, RotateZ }