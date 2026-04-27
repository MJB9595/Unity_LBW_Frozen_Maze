using UnityEngine;

public static class GameProgress
{
    // 엔진 작동 여부를 저장하는 전역 변수
    // 씬이 변경되어도 초기화되지 않고 유지됩니다.
    public static bool isEngineFixed = false;

    // 씬 전환 시 플레이어의 마지막 위치를 기억하기 위한 변수
    public static Vector3 lastPlayerPosition;
    public static bool hasSavedPosition = false;
}
