public interface IPlayerSkill
{
    // 스킬을 실행할 수 있는지 확인
    bool CanExecute(SkillContext context);

    // 스킬 동작 실행
    void Execute(SkillContext context);
}