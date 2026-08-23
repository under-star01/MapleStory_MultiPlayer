public interface ILocalPlayerUI
{
    /// <summary>
    /// 로컬 플레이어가 생성되거나 권한을 얻었을 때
    /// 필요한 플레이어 참조를 연결합니다.
    /// </summary>
    void Bind(LocalPlayerContext context);

    /// <summary>
    /// 로컬 플레이어 연결이 해제될 때
    /// 이벤트 구독과 참조를 정리합니다.
    /// </summary>
    void Unbind();
}