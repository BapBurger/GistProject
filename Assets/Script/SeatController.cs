using UnityEngine;

public class SeatController : MonoBehaviour
{
    [Header("1. 소스 연결")]
    public GameObject carSourceObj;
    public bool enableCar = true;
    public GameObject shipSourceObj;
    public bool enableShip = true;

    private IMotionSource carSource;
    private IMotionSource shipSource;

    [Header("2. 모드 설정 (핵심!)")]
    [Tooltip("체크하면 자동차 움직임을 상쇄(반대로 움직임)하여 배 느낌만 남깁니다.")]
    public bool useMotionCancellation = false; // ★ 여기가 핵심입니다!

    [Header("3. 입력 게인 (Input Gain)")]
    [Range(0.0f, 5.0f)]
    public float masterGain = 0.5f;

    [Tooltip("축별 미세 조절")]
    public float surgeScale = 1.0f;
    public float swayScale = 1.0f;
    public float heaveScale = 1.0f;

    [Header(">> 실시간 모니터 (최종 계산된 G값)")]
    public float monitorSurgeG = 0.0f;
    public float monitorSwayG = 0.0f;
    public float monitorHeaveG = 0.0f;

    [Header("4. 모터 반응 속도")]
    public float actuatorSpeed = 60.0f;

    [Header("5. 모션 필터")]
    public float washoutRate = 1.0f;
    public float deadZone = 0.01f;

    [Header("6. 시트 물리 한계")]
    public float tiltSpeed = 10.0f;
    public float maxTiltLimit = 20.0f;

    [Header("7. 모션 매핑")]
    public float slideGain = 1.0f;
    public float heaveGain = 1.0f;
    public float bolsterGain = 30.0f;

    [Header("8. 볼스터 설정")]
    public bool useDirectBolster = true;
    public bool invertLeftBolster = true;

    [Header("9. 부품 인덱스")]
    public int wholeSlideIndex = 0;
    public int backSeatIndex = 1;
    public int rightBolsterIndex = 2;
    public int leftBolsterIndex = 3;
    public int rightBackBolsterIndex = 4;
    public int leftBackBolsterIndex = 5;
    public int wholeLiftIndex = 6;

    [Header("10. 시트 부품")]
    public SeatPart[] seatParts;

    // 필터 변수
    private float filteredSurge = 0f;
    private float filteredSway = 0f;
    private float filteredHeave = 0f;

    void Start()
    {
        foreach (var part in seatParts) part.Initialize();
        UpdateSourceConnection();
    }

    public void UpdateSourceConnection()
    {
        if (carSourceObj != null) carSource = carSourceObj.GetComponent<IMotionSource>();
        if (shipSourceObj != null) shipSource = shipSourceObj.GetComponent<IMotionSource>();
    }

    void Update()
    {
        ProcessSimulation();
    }

    void ProcessSimulation()
    {
        // 1. 데이터 수집
        float carSurge = 0f, carSway = 0f, carHeave = 0f;
        float shipSurge = 0f, shipSway = 0f, shipHeave = 0f;

        if (enableCar && carSource != null)
        {
            carSurge = carSource.GetSurgeG();
            carSway = carSource.GetSwayG();
            carHeave = carSource.GetHeaveG();
        }

        if (enableShip && shipSource != null)
        {
            shipSurge = shipSource.GetSurgeG();
            shipSway = shipSource.GetSwayG();
            shipHeave = shipSource.GetHeaveG();
        }

        // 2. ★ 로직 분기 (더하기 vs 빼기)
        float targetSurge, targetSway, targetHeave;

        if (useMotionCancellation)
        {
            // [모드 ON] 캔슬링 모드: 배(원하는 것) - 차(없애고 싶은 것)
            targetSurge = shipSurge - carSurge;
            targetSway = shipSway - carSway;
            targetHeave = shipHeave - carHeave;
        }
        else
        {
            // [모드 OFF] 일반 시뮬레이터: 배 + 차
            targetSurge = shipSurge + carSurge;
            targetSway = shipSway + carSway;
            targetHeave = shipHeave + carHeave;
        }

        // 3. Gain 적용
        targetSurge = targetSurge * masterGain * surgeScale;
        targetSway = targetSway * masterGain * swayScale;
        targetHeave = targetHeave * masterGain * heaveScale;

        // 모니터링 업데이트
        monitorSurgeG = targetSurge;
        monitorSwayG = targetSway;
        monitorHeaveG = targetHeave;

        // 4. 필터링 (Washout)
        if (Mathf.Abs(targetSurge) < deadZone) targetSurge = 0;
        if (Mathf.Abs(targetHeave) < deadZone) targetHeave = 0;

        filteredSurge = Mathf.Lerp(filteredSurge, targetSurge, Time.deltaTime * tiltSpeed);
        filteredHeave = Mathf.Lerp(filteredHeave, targetHeave, Time.deltaTime * tiltSpeed);

        filteredSurge = Mathf.Lerp(filteredSurge, 0f, Time.deltaTime * washoutRate);
        filteredHeave = Mathf.Lerp(filteredHeave, 0f, Time.deltaTime * washoutRate);

        // 5. 볼스터 처리
        float swayToUse = useDirectBolster ? targetSway : Mathf.Lerp(filteredSway, targetSway, Time.deltaTime * tiltSpeed);

        float rightTarget = (swayToUse > 0) ? swayToUse * bolsterGain : 0;
        float leftRaw = (swayToUse < 0) ? -swayToUse * bolsterGain : 0;
        float leftTarget = invertLeftBolster ? -leftRaw : leftRaw;

        ApplyMotion(rightBolsterIndex, rightTarget, false);
        ApplyMotion(leftBolsterIndex, leftTarget, false);
        ApplyMotion(rightBackBolsterIndex, -rightTarget, false);
        ApplyMotion(leftBackBolsterIndex, -leftTarget, false);

        // 6. 모션 적용
        float clampedSurge = Mathf.Clamp(filteredSurge, -1.0f, 1.0f);
        float targetPitch = Mathf.Asin(clampedSurge) * Mathf.Rad2Deg;

        ApplyMotion(backSeatIndex, Mathf.Clamp(targetPitch, -maxTiltLimit, maxTiltLimit), true);
        ApplyMotion(wholeSlideIndex, filteredSurge * slideGain, false);
        ApplyMotion(wholeLiftIndex, filteredHeave * heaveGain, false);
    }

    // ... (ApplyMotion, UpdateTransform, SeatPart 클래스는 기존과 동일하게 유지) ...
    void ApplyMotion(int index, float targetValue, bool isPhysicsAngle)
    {
        if (index < 0 || index >= seatParts.Length) return;
        SeatPart part = seatParts[index];
        float finalTarget = part.initialValue + targetValue;
        finalTarget = Mathf.Clamp(finalTarget, part.minLimit, part.maxLimit);
        part.currentValue = Mathf.MoveTowards(part.currentValue, finalTarget, Time.deltaTime * actuatorSpeed);
        UpdateTransform(part);
    }
    void UpdateTransform(SeatPart part)
    {
        if (part.targetTransform == null) return;
        switch (part.moveType)
        {
            case MoveType.SlideZ: Vector3 pZ = part.targetTransform.localPosition; pZ.z = part.currentValue; part.targetTransform.localPosition = pZ; break;
            case MoveType.SlideY: Vector3 pY = part.targetTransform.localPosition; pY.y = part.currentValue; part.targetTransform.localPosition = pY; break;
            case MoveType.RotateX: part.targetTransform.localRotation = Quaternion.Euler(part.currentValue, part.fixedY, part.fixedZ); break;
            case MoveType.RotateY: part.targetTransform.localRotation = Quaternion.Euler(part.fixedX, part.currentValue, part.fixedZ); break;
            case MoveType.RotateZ: part.targetTransform.localRotation = Quaternion.Euler(part.fixedX, part.fixedY, part.currentValue); break;
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
    public float currentValue;
    [HideInInspector] public float initialValue;
    [HideInInspector] public float fixedX, fixedY, fixedZ;
    public void Initialize()
    {
        if (targetTransform == null) return;
        fixedX = targetTransform.localEulerAngles.x; fixedY = targetTransform.localEulerAngles.y; fixedZ = targetTransform.localEulerAngles.z;
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