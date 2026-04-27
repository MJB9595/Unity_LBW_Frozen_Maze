using UnityEngine;
using System.Collections;
using System.Collections.Generic; // 리스트 사용을 위해 필요합니다.

public class MultiSteamController : MonoBehaviour
{
    [Header("파티클 그룹 설정")]
    public List<ParticleSystem> leftSteams = new List<ParticleSystem>();  // 왼쪽 20개 칸
    public List<ParticleSystem> rightSteams = new List<ParticleSystem>(); // 오른쪽 6개 칸

    [Header("시간 설정 (초 단위)")]
    public float activeTime = 2.0f;  // 뿜어져 나오는 시간
    public float restTime = 2.0f;    // 전체가 다 안 나오는 쉬는 시간

    void Start()
    {
        // 1. 모든 파티클의 Looping과 Play on Awake를 코드로 자동 최적화합니다.
        foreach (var ps in leftSteams) { SetupParticle(ps); }
        foreach (var ps in rightSteams) { SetupParticle(ps); }

        // 2. 번갈아 가며 실행하는 루틴 시작
        StartCoroutine(SteamRoutine());
    }

    void SetupParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        var main = ps.main;
        main.loop = false;         // 코드가 제어해야 하므로 루프는 끕니다.
        main.playOnAwake = false;
        ps.Stop();
    }

    IEnumerator SteamRoutine()
    {
        while (true) // 무한 반복
        {
            // --- 왼쪽 그룹 20개 발사 ---
            foreach (var ps in leftSteams) { if (ps != null) ps.Play(); }
            yield return new WaitForSeconds(activeTime);
            foreach (var ps in leftSteams) { if (ps != null) ps.Stop(); }

            yield return new WaitForSeconds(restTime); // 쉬는 시간

            // --- 오른쪽 그룹 6개 발사 ---
            foreach (var ps in rightSteams) { if (ps != null) ps.Play(); }
            yield return new WaitForSeconds(activeTime);
            foreach (var ps in rightSteams) { if (ps != null) ps.Stop(); }

            yield return new WaitForSeconds(restTime); // 쉬는 시간
        }
    }
}