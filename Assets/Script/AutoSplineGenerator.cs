using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Linq;

public class AutoSplineGenerator : MonoBehaviour
{
    [Header("1. 여기에 트랙 조각들을 순서대로 넣으세요")]
    public List<Transform> trackPieces;

    [Header("2. Spline Container를 여기에 넣으세요")]
    public SplineContainer targetSpline;

    [Header("옵션")]
    [Tooltip("체크하면 트랙의 양 끝점도 연결합니다.")]
    public bool useEdgePoints = true;

    [Header("필터링 설정 (중요!)")]
    [Tooltip("도로 메쉬의 이름이나 재질에 포함된 단어를 입력하세요. (예: Road, Asphalt, Track)\n빈칸이면 모든 메쉬를 포함합니다.")]
    public string roadKeyword = "Road";

    [ContextMenu("Generate Spline Now")]
    public void GeneratePath()
    {
        if (targetSpline == null)
        {
            Debug.LogError("Spline Container가 연결되지 않았습니다!");
            return;
        }

        if (trackPieces == null || trackPieces.Count == 0)
        {
            Debug.LogError("trackPieces가 비어있습니다!");
            return;
        }

        targetSpline.Spline = new Spline();

        // 흐름 파악을 위한 이전 위치 저장용 변수
        Vector3? previousPos = null;

        foreach (var piece in trackPieces)
        {
            if (piece == null) continue;

            // 1. 진짜 '도로' 부분의 Bounds만 가져오기 (투명한 배경 무시)
            if (!TryGetRoadBounds(piece, roadKeyword, out Bounds wb))
            {
                // 실패 시 그냥 피벗 위치 사용
                AddKnotSmart(piece.position, piece.rotation);
                previousPos = piece.position;
                continue;
            }

            // 2. 진행 방향(Forward) 자동 보정 로직
            // 일단 오브젝트의 forward를 가져옴
            Vector3 logicalForward = piece.forward.normalized;

            // 이전 조각이 있다면, 흐름(Flow) 벡터를 계산해서 비교
            if (previousPos.HasValue)
            {
                // 이전 위치(탈출구) -> 현재 위치(중심)로 가는 방향이 곧 '트랙의 흐름'
                Vector3 flowDirection = (wb.center - previousPos.Value).normalized;

                // 내 forward가 흐름과 반대(내적값이 음수)라면? -> "아, 이 조각은 거꾸로 돌려진 놈이다!"
                // (90도 꺾인 경우에도 흐름과 비슷하면 통과, 정반대면 뒤집음)
                if (Vector3.Dot(flowDirection, logicalForward) < -0.1f)
                {
                    logicalForward = -logicalForward; // 계산용 방향을 반대로 뒤집음
                }
            }

            // 3. 중심 및 입/출구 계산
            Vector3 center = wb.center;

            // Bounds를 (보정된) 논리적 Forward 방향으로 투영해서 min/max 지점 찾기
            float minS, maxS;
            GetMinMaxAlongDirection(wb, logicalForward, out minS, out maxS);
            float centerS = Vector3.Dot(center, logicalForward);

            // min이 진입(Entry), max가 탈출(Exit)이 됨 (방향이 이미 보정되었으므로)
            Vector3 entry = center + logicalForward * (minS - centerS);
            Vector3 exit = center + logicalForward * (maxS - centerS);

            // 4. 점 찍기
            if (useEdgePoints) AddKnotSmart(entry, piece.rotation);
            AddKnotSmart(center, piece.rotation);
            if (useEdgePoints) AddKnotSmart(exit, piece.rotation);

            // 다음 루프를 위해 현재의 '탈출구' 위치를 저장 (다음 놈이 이걸 보고 방향 잡음)
            previousPos = exit;
        }

        // Tangent 모드 설정 (부드럽게 연결)
        for (int i = 0; i < targetSpline.Spline.Count; i++)
            targetSpline.Spline.SetTangentMode(i, TangentMode.Continuous);

        targetSpline.Spline.Closed = true;
        Debug.Log($"✅ 스마트 트랙 생성 완료! ({targetSpline.Spline.Count}개 포인트)");
    }

    // 도로 이름(키워드)이 포함된 렌더러만 골라서 Bounds 계산하는 함수
    static bool TryGetRoadBounds(Transform root, string keyword, out Bounds bounds)
    {
        // 모든 렌더러(메쉬) 가져오기
        var renderers = root.GetComponentsInChildren<Renderer>();

        // 필터링된 렌더러 리스트
        var validRenderers = new List<Renderer>();

        foreach (var r in renderers)
        {
            // 키워드가 비어있으면 다 넣고, 있으면 이름이나 재질 이름 체크
            if (string.IsNullOrEmpty(keyword) ||
                r.name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase) ||
                (r.sharedMaterial != null && r.sharedMaterial.name.Contains(keyword, System.StringComparison.OrdinalIgnoreCase)))
            {
                validRenderers.Add(r);
            }
        }

        if (validRenderers.Count == 0)
        {
            // 혹시 키워드로 못 찾았으면, 에라 모르겠다 전체 다 포함 (Fallback)
            if (renderers.Length > 0) validRenderers.AddRange(renderers);
            else
            {
                bounds = default;
                return false;
            }
        }

        // 선택된 녀석들의 합집합 크기(Bounds) 계산
        bounds = validRenderers[0].bounds;
        for (int i = 1; i < validRenderers.Count; i++)
            bounds.Encapsulate(validRenderers[i].bounds);

        return true;
    }

    // 특정 방향(dir)으로 Bounds를 투영해서 최소/최대 위치 찾기 (수학 공식)
    static void GetMinMaxAlongDirection(Bounds b, Vector3 dir, out float minS, out float maxS)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;

        // 박스의 8개 꼭짓점 좌표
        Vector3[] corners = new Vector3[8]
        {
            c + new Vector3( e.x,  e.y,  e.z), c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z), c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z), c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z), c + new Vector3(-e.x, -e.y, -e.z),
        };

        minS = float.PositiveInfinity;
        maxS = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            float s = Vector3.Dot(corners[i], dir);
            if (s < minS) minS = s;
            if (s > maxS) maxS = s;
        }
    }

    void AddKnotSmart(Vector3 worldPos, Quaternion worldRot)
    {
        var localPos = targetSpline.transform.InverseTransformPoint(worldPos);
        var localRot = Quaternion.Inverse(targetSpline.transform.rotation) * worldRot;

        // 점이 너무 가까우면(중복) 건너뛰기
        if (targetSpline.Spline.Count > 0)
        {
            var lastPos = targetSpline.Spline.Last().Position;
            if (Vector3.Distance(localPos, lastPos) < 0.05f) return;
        }

        BezierKnot knot = new BezierKnot(localPos);
        knot.Rotation = localRot;
        targetSpline.Spline.Add(knot);
    }
}