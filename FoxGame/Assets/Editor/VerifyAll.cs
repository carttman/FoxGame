using UnityEditor;
using UnityEngine;

// 3~6, 8단계 검증을 한 번에 실행한다 (씬을 수정하지 않음). 결과는 Console에 "검증 통과/실패"로 출력.
// 메뉴: Tools > Fox > 전체 검증
// 배치: Unity.exe -batchmode -projectPath . -executeMethod VerifyAll.Run -quit
public static class VerifyAll
{
    [MenuItem("Tools/Fox/전체 검증")]
    public static void Run()
    {
        LevelBuilder.VerifyWalk();
        AnimationSetup.Verify();
        ItemSetup.Verify();
        ResultSetup.Verify();
        CrumbleSetup.Verify();
        Debug.Log("[VerifyAll] 완료");
    }
}
