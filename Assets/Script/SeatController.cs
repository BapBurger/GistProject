using UnityEngine;

public class SeatController : MonoBehaviour
{
    [Header("1. 소스 연결 & 토글 (핵심 기능)")]
    public GameObject carSourceObj;  // 자동차 오브젝트 연결
    public bool enableCar = true;    // [토글] 체크하면 자동차 G값 반영

    public GameObject shipSourceObj; // 배 오브젝트 연결
    public bool enableShip = true;   // [토글] 체크하면 배 G값 반영

    private IMotionSource carSource;
    private IMotionSource shipSource;

    [Header("2. 모터 반응 속도")]
    [Tooltip("값이 클수록 빠르지만 너무 크면 뚝뚝 끊김 (추천: 50 ~ 100)")]
    public float actuatorSpeed = 60.0f;

    [Header("3. 모션 필터")]
    public float washoutRate = 1.0f;
    public float deadZone = 0.01f;

    [Header("4. 물리 튜닝")]
    public float tiltSpeed = 20.0f;
    public float maxTiltLimit = 20.0f;

    [Header("5. 모션 게인 (강도)")]
    public float slideGain = 0.5f;
    public float heaveGain = 0.1f;
    public float bolsterGain = 8.0f;

    [Header("6. 볼스터 설정")]
    public bool useDirectBolster = true;
    public bool invertLeftBolster = true; // [중요] 아까 이게 켜져야 잘 된다고 하셨으므로 기본값 true

    [Header("7. 부품 인덱스")]
    public int wholeSlideIndex = 0;
    public int backSeatIndex = 1;
    public int rightBolsterIndex = 2;
    public int leftBolsterIndex = 3;
    public int rightBackBolsterIndex = 4;
    public int leftBackBolsterIndex = 5;
    public int wholeLiftIndex = 6;

    [Header("8. 시트 부품")]
    public SeatPart[] seatParts;

    // 필터 변수
    private float filteredSurge = 0f;
    private float filteredSway = 0f;
    private float filteredHeave = 0f;

    void Start()
    {
        foreach (var part in seatParts) part.Initialize();

        // 1. 자동차 소스 찾기
        if (carSourceObj != null)
        {
            carSource = carSourceObj.GetComponent<IMotionSource>();
            if (carSource == null) Debug.LogError("Car Object에 IMotionSource 스크립트가 없습니다.");
        }

        // 2. 배 소스 찾기
        if (shipSourceObj != null)
        {
            shipSource = shipSourceObj.GetComponent<IMotionSource>();
            if (shipSource == null) Debug.LogError("Ship Object에 IMotionSource 스크립트가 없습니다.");
        }
    }

    void Update()
    {
        ProcessSimulation();
    }

    void ProcessSimulation()
    {
        // ---------------------------------------------------------
        // 1. [Total G 계산] 토글 상태에 따라 G값을 합산합니다.
        // ---------------------------------------------------------
        float totalSurge = 0f;
        float totalSway = 0f;
        float totalHeave = 0f;

        // (A) 자동차 G값 더하기
        if (enableCar && carSource != null)
        {
            totalSurge += carSource.GetSurgeG();
            totalSway += carSource.GetSwayG();
            totalHeave += carSource.GetHeaveG();
        }

        // (B) 배 G값 더하기
        if (enableShip && shipSource != null)
        {
            totalSurge += shipSource.GetSurgeG();
            totalSway += shipSource.GetSwayG();
            totalHeave += shipSource.GetHeaveG();
        }

        // ---------------------------------------------------------
        // 2. 필터링 로직 (기존과 동일)
        // ---------------------------------------------------------
        if (Mathf.Abs(totalSurge) < deadZone) totalSurge = 0;
        if (Mathf.Abs(totalHeave) < deadZone) totalHeave = 0;

        filteredSurge = Mathf.Lerp(filteredSurge, totalSurge, Time.deltaTime * tiltSpeed);
        filteredHeave = Mathf.Lerp(filteredHeave, totalHeave, Time.deltaTime * tiltSpeed);

        filteredSurge = Mathf.Lerp(filteredSurge, 0f, Time.deltaTime * washoutRate);
        filteredHeave = Mathf.Lerp(filteredHeave, 0f, Time.deltaTime * washoutRate);

        // ---------------------------------------------------------
        // 3. 볼스터 처리
        // ---------------------------------------------------------
        float swayToUse = useDirectBolster ? totalSway : Mathf.Lerp(filteredSway, totalSway, Time.deltaTime * tiltSpeed);

        float rightTarget = (swayToUse > 0) ? swayToUse * bolsterGain : 0;
        float leftRaw = (swayToUse < 0) ? -swayToUse * bolsterGain : 0;
        float leftTarget = invertLeftBolster ? -leftRaw : leftRaw;

        ApplyMotion(rightBolsterIndex, rightTarget, false);
        ApplyMotion(leftBolsterIndex, leftTarget, false);
        ApplyMotion(rightBackBolsterIndex, -rightTarget, false);
        ApplyMotion(leftBackBolsterIndex, -leftTarget, false);

        // ---------------------------------------------------------
        // 4. 나머지 파트 적용
        // ---------------------------------------------------------
        float clampedSurge = Mathf.Clamp(filteredSurge, -1.0f, 1.0f);
        float targetPitch = Mathf.Asin(clampedSurge) * Mathf.Rad2Deg;

        ApplyMotion(backSeatIndex, Mathf.Clamp(targetPitch, -maxTiltLimit, maxTiltLimit), true);
        ApplyMotion(wholeSlideIndex, filteredSurge * slideGain, false);
        ApplyMotion(wholeLiftIndex, filteredHeave * heaveGain, false);
    }

    void ApplyMotion(int index, float targetValue, bool isPhysicsAngle)
    {
        if (index < 0 || index >= seatParts.Length) return;

        SeatPart part = seatParts[index];
        float finalTarget = part.initialValue + targetValue;

        finalTarget = Mathf.Clamp(finalTarget, part.minLimit, part.maxLimit);

        // 부드러운 모터 움직임
        part.currentValue = Mathf.MoveTowards(part.currentValue, finalTarget, Time.deltaTime * actuatorSpeed);

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

// SeatPart 클래스와 Enum은 파일 아래에 그대로 두세요.
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