using UnityEngine;
using UnityEngine.UI;

public class SimulationUI : MonoBehaviour
{
    [Header("--- 연결 대상 ---")]
    public CarPhysicsController carController;
    public SeatController seatController;
    // ▼▼▼ [추가] 배 스크립트 연결 변수
    public ShipPhysicsController shipController;

    [Header("--- 키 입력 시각화 ---")]
    public Image wKeyImage;
    public Image sKeyImage;
    public Color activeColor = Color.red;
    public Color inactiveColor = Color.gray;

    [Header("--- UI 텍스트 ---")]
    public Text speedText;
    public Text gForceText;
    public Text seatStatusText;

    [Header("--- UI 슬라이더 ---")]
    public Slider surgeSlider;
    public Slider swaySlider;
    public Slider rightBolsterSlider;
    public Slider leftBolsterSlider;

    // ▼▼▼ [추가] 파도(Heave) 슬라이더 (없으면 연결 안 해도 됨)
    public Slider heaveSlider;

    void Update()
    {
        if (seatController == null) return;

        UpdateVehicleInfo(); // 이름 변경: CarInfo -> VehicleInfo
        UpdateSeatInfo();
        UpdateInputVisuals();
    }

    void UpdateInputVisuals()
    {
        if (wKeyImage != null) wKeyImage.color = Input.GetKey(KeyCode.W) ? activeColor : inactiveColor;
        if (sKeyImage != null) sKeyImage.color = Input.GetKey(KeyCode.S) ? activeColor : inactiveColor;
    }

    void UpdateVehicleInfo()
    {
        float speed = 0f;
        float lonG = 0f;
        float latG = 0f;
        float heaveG = 0f; // [추가] 파도 G값

        // 1. 배 모드인지 확인 (배 오브젝트가 켜져 있는지)
        if (shipController != null && shipController.gameObject.activeInHierarchy)
        {
            // 배 속도는 Rigidbody에서 가져옴 (magnitude * 3.6 = km/h)
            if (shipController.GetComponent<Rigidbody>() != null)
                speed = shipController.GetComponent<Rigidbody>().velocity.magnitude * 3.6f;

            lonG = shipController.GetSurgeG();
            latG = shipController.GetSwayG();
            heaveG = shipController.GetHeaveG(); // 배는 파도 값이 있음!
        }
        // 2. 자동차 모드인지 확인
        else if (carController != null && carController.gameObject.activeInHierarchy)
        {
            speed = carController.currentSpeed * 3.6f;
            lonG = carController.longitudinalG;
            latG = carController.lateralG;
            heaveG = 0f; // 자동차는 평지라 0
        }

        // 텍스트 업데이트
        speedText.text = $"SPEED: {speed:F0} km/h";

        // ▼▼▼ [수정] Heave 항목 추가
        gForceText.text = $"[G-Force]\n" +
                          $"Surge (앞뒤): {lonG:F3} G\n" +
                          $"Sway  (좌우): {latG:F3} G\n" +
                          $"Heave (파도): {heaveG:F3} G";

        // 슬라이더 업데이트
        if (surgeSlider != null) surgeSlider.value = lonG;
        if (swaySlider != null) swaySlider.value = latG;
        if (heaveSlider != null) heaveSlider.value = heaveG; // 파도 슬라이더가 있다면 반영
    }

    void UpdateSeatInfo()
    {
        float slideVal = GetSeatValue(seatController.wholeSlideIndex);
        float pitchVal = GetSeatValue(seatController.backSeatIndex);
        float rightBolsterVal = GetSeatValue(seatController.rightBolsterIndex);
        float leftBolsterVal = GetSeatValue(seatController.leftBolsterIndex);

        // ▼▼▼ [추가] 시트 높이(Lift) 값 가져오기
        float liftVal = GetSeatValue(seatController.wholeLiftIndex);

        // ▼▼▼ [수정] Lift 항목 추가
        seatStatusText.text = $"[Seat Status]\n" +
                              $"Slide : {slideVal:F1}\n" +
                              $"Lift  : {liftVal:F1}\n" + // 시트가 위아래로 얼마나 움직이는지 표시
                              $"Pitch : {pitchVal:F1}\n" +
                              $"R-Bolster: {rightBolsterVal:F1}\n" +
                              $"L-Bolster: {leftBolsterVal:F1}";

        if (rightBolsterSlider != null) rightBolsterSlider.value = rightBolsterVal;
        if (leftBolsterSlider != null) leftBolsterSlider.value = leftBolsterVal;
    }

    float GetSeatValue(int index)
    {
        if (index >= 0 && index < seatController.seatParts.Length)
        {
            return seatController.seatParts[index].currentValue;
        }
        return 0f;
    }
}