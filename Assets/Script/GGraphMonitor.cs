using UnityEngine;
using System.Collections.Generic;

public class GGraphMonitor : MonoBehaviour
{
    [Header("1. 데이터 소스 연결")]
    public DualMotionMixer mixer; // 아까 만든 믹서기를 여기에 넣으세요!

    [Header("2. 그래프 설정")]
    public int maxDataPoints = 100; // 그래프 길이 (점 개수)
    public float graphHeight = 5.0f; // 그래프 높이 배율 (G값이 작으면 키우세요)
    public float updateInterval = 0.05f; // 갱신 주기 (작을수록 부드러움)

    [Header("3. 보고 싶은 G축 선택")]
    public GraphType graphType = GraphType.Surge; // 인스펙터에서 바꿀 수 있음

    [Header("4. 라인 렌더러 연결 (자동으로 색칠됨)")]
    public LineRenderer carLine;   // 자동차용 (Red)
    public LineRenderer shipLine;  // 배용 (Blue)
    public LineRenderer totalLine; // 합계용 (Green)

    // 내부 데이터 저장용 리스트
    private List<float> carData = new List<float>();
    private List<float> shipData = new List<float>();
    private List<float> totalData = new List<float>();
    private float timer;

    void Start()
    {
        // 선 굵기랑 색깔 초기화 (귀찮을까봐 코드로 자동 설정해둠)
        SetupLine(carLine, Color.red);
        SetupLine(shipLine, Color.cyan);
        SetupLine(totalLine, Color.green);
    }

    void Update()
    {
        if (mixer == null) return;

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0;
            UpdateGraphData();
            DrawGraph();
        }
    }

    void UpdateGraphData()
    {
        float c = 0, s = 0, t = 0;

        // 선택한 축(Surge, Sway, Heave)에 따라 데이터를 가져옴
        switch (graphType)
        {
            case GraphType.Surge: // 앞뒤 가속도
                if (mixer.carScript != null) c = mixer.carScript.GetSurgeG();
                if (mixer.shipScript != null) s = mixer.shipScript.GetSurgeG();
                t = mixer.GetSurgeG();
                break;

            case GraphType.Sway: // 좌우 가속도
                if (mixer.carScript != null) c = mixer.carScript.GetSwayG();
                if (mixer.shipScript != null) s = mixer.shipScript.GetSwayG();
                t = mixer.GetSwayG();
                break;

            case GraphType.Heave: // 상하 가속도
                if (mixer.carScript != null) c = mixer.carScript.GetHeaveG();
                if (mixer.shipScript != null) s = mixer.shipScript.GetHeaveG();
                t = mixer.GetHeaveG();
                break;
        }

        // 리스트에 추가하고, 너무 길어지면 옛날 것 삭제
        AddData(carData, c);
        AddData(shipData, s);
        AddData(totalData, t);
    }

    void AddData(List<float> list, float value)
    {
        list.Add(value);
        if (list.Count > maxDataPoints) list.RemoveAt(0);
    }

    void DrawGraph()
    {
        DrawLine(carLine, carData);
        DrawLine(shipLine, shipData);
        DrawLine(totalLine, totalData);
    }

    void DrawLine(LineRenderer line, List<float> data)
    {
        if (line == null) return;
        line.positionCount = data.Count;

        for (int i = 0; i < data.Count; i++)
        {
            // X축: 시간(순서), Y축: G값 * 배율
            float x = i * 0.1f;
            float y = data[i] * graphHeight;

            // 그래프를 현재 오브젝트 위치 기준으로 그림
            Vector3 pos = transform.position + new Vector3(x, y, 0);
            line.SetPosition(i, pos);
        }
    }

    void SetupLine(LineRenderer line, Color color)
    {
        if (line == null) return;
        line.startWidth = 0.1f;
        line.endWidth = 0.1f;
        line.material = new Material(Shader.Find("Sprites/Default")); // 기본 쉐이더
        line.startColor = color;
        line.endColor = color;
    }

    public enum GraphType { Surge, Sway, Heave }
}