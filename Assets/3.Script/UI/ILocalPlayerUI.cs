public interface ILocalPlayerUI
{
    // 로컬 플레이어 참조 연결
    void Bind(LocalPlayerContext context);

    // 로컬 플레이어 연결 해제 및 정리
    void Unbind();
}