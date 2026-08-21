public interface IPlayerSkill
{
    /// <summary>
    /// 현재 상황에서 이 스킬을 실행할 수 있는지 확인합니다.
    /// </summary>
    bool CanExecute(SkillContext context);

    /// <summary>
    /// 스킬의 실제 동작을 실행합니다.
    /// </summary>
    void Execute(SkillContext context);
}