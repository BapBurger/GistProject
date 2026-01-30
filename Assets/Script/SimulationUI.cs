using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimulationUI : MonoBehaviour
{
    [Header("--- 연결 대상 ---")]
    public CarPhysicsController carController;
    public ShipPhysicsController shipController;
    public SeatController seatController;

    [Header("--- UI 텍스트 (TMP) ---")]
    public TMP_Text speedText;
    public TMP_Text gForceText;
    public TMP_Text seatStatusText;
    public TMP_Text modeTitleText;

    [Header("--- UI 슬라이더 ---")]
    public Slider surgeSlider;
    public Slider swaySlider;
    public Slider heaveSlider;
    public Slider rightBolsterSlider;
    public Slider leftBolsterSlider;

    // ▼▼▼ [신규 추가] WASD 입력 UI ▼▼▼
    [Header("--- WASD 입력 피드백 ---")]
    public Image keyImageW; // W키 이미지
    public Image keyImageA; // A키 이미지
    public Image keyImageS; // S키 이미지
    public Image keyImageD; // D키 이미지

    [Tooltip("평상시 색상 (반투명 권장)")]
    public Color normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 회색 반투명

    [Tooltip("눌렀을 때 색상 (밝은색 권장)")]
    public Color pressedColor = new Color(1f, 0.8f, 0f, 1f); // 노란색
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void Update()
    {
        UpdateVehicleInfo();
        UpdateSeatInfo();
        UpdateInputFeedback(); // [추가] 매 프레임 입력 확인
    }

    // ▼▼▼ [신규 추가] 입력 감지 및 색상 변경 함수 ▼▼▼
    void UpdateInputFeedback()
    {
        // W키 확인 (누르고 있으면 pressedColor, 아니면 normalColor)
        if (keyImageW != null)
            keyImageW.color = Input.GetKey(KeyCode.W) ? pressedColor : normalColor;

        // A키 확인
        if (keyImageA != null)
            keyImageA.color = Input.GetKey(KeyCode.A) ? pressedColor : normalColor;

        // S키 확인
        if (keyImageS != null)
            keyImageS.color = Input.GetKey(KeyCode.S) ? pressedColor : normalColor;

        // D키 확인
        if (keyImageD != null)
            keyImageD.color = Input.GetKey(KeyCode.D) ? pressedColor : normalColor;
    }
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    void UpdateVehicleInfo()
    {
        float speed = 0f;
        float lonG = 0f;
        float latG = 0f;
        float heaveG = 0f;
        string currentMode = "NONE";

        // 1. 배 모드
        if (shipController != null && shipController.gameObject.activeInHierarchy)
        {
            currentMode = "SHIP MODE";
            if (shipController.GetComponent<Rigidbody>())
                speed = shipController.GetComponent<Rigidbody>().velocity.magnitude * 3.6f;

            lonG = shipController.GetSurgeG();
            latG = shipController.GetSwayG();
            heaveG = shipController.GetHeaveG();
        }
        // 2. 자동차 모드
        else if (carController != null && carController.gameObject.activeInHierarchy)
        {
            currentMode = "CAR MODE";
            speed = carController.currentSpeed * 3.6f;
            lonG = carController.longitudinalG;
            latG = carController.lateralG;
            heaveG = 0f;
        }

        // 텍스트 업데이트
        if (modeTitleText) modeTitleText.text = currentMode;
        if (speedText) speedText.text = $"SPEED: {speed:F0} km/h";
        if (gForceText) gForceText.text = $"Surge: {lonG:F2} G\nSway : {latG:F2} G\nHeave: {heaveG:F2} G";

        // 슬라이더 업데이트
        if (surgeSlider) surgeSlider.value = lonG;
        if (swaySlider) swaySlider.value = latG;
        if (heaveSlider) heaveSlider.value = heaveG;
    }

    void UpdateSeatInfo()
    {
        if (seatController == null) return;

        float slideVal = GetSeatValue(seatController.wholeSlideIndex);
        float liftVal = GetSeatValue(seatController.wholeLiftIndex);
        float pitchVal = GetSeatValue(seatController.backSeatIndex);
        float rightBolsterVal = GetSeatValue(seatController.rightBolsterIndex);
        float leftBolsterVal = GetSeatValue(seatController.leftBolsterIndex);

        if (seatStatusText)
        {
            seatStatusText.text = $"[Seat Status]\n" +
                                  $"Slide : {slideVal:F1}\n" +
                                  $"Lift  : {liftVal:F1}\n" +
                                  $"Pitch : {pitchVal:F1}\n" +
                                  $"R-Bolster: {rightBolsterVal:F1}\n" +
                                  $"L-Bolster: {leftBolsterVal:F1}";
        }

        if (rightBolsterSlider != null) rightBolsterSlider.value = rightBolsterVal;
        if (leftBolsterSlider != null) leftBolsterSlider.value = leftBolsterVal;
    }

    float GetSeatValue(int index)
    {
        if (seatController != null && index >= 0 && index < seatController.seatParts.Length)
        {
            return seatController.seatParts[index].currentValue;
        }
        return 0f;
    }
}