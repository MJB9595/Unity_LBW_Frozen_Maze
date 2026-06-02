using UnityEngine;

/// <summary>
/// 이 마커가 붙은(또는 부모에 붙은) 오브젝트는 골렘이 닿으면 "집"과 동일하게
/// 크랙 데칼이 아니라 실제 fracture(붕괴)로 처리된다.
/// BossDestruction.IsStaticCrackObject 에서 이 마커를 우선 검사한다.
/// </summary>
public class GolemCollapsible : MonoBehaviour { }
