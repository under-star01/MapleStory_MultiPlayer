using UnityEngine;

public interface IQuickSlotCommand
{
    /// <summary>
    /// 슬롯에 등록된 행동을 실행합니다.
    /// </summary>
    /// <param name="inputDirection">
    /// 실행 순간의 방향 입력입니다.
    /// 점프 스킬은 아래 입력 여부를 판단할 때 사용합니다.
    /// </param>
    /// <returns>행동 실행에 성공하면 true를 반환합니다.</returns>
    bool Execute(Vector2 inputDirection);
}