// 레이어 번호 상수. 에디터 메뉴 Tower > 프로젝트 초기 구성에서 같은 번호로 등록한다.
public class GameLayers
{
    public const int Player = 6;
    public const int Monster = 7;
    public const int Wall = 8;
    // 픽셀 처리 없이 매끈하게 그리는 월드 글자(PixelView)
    public const int WorldText = 9;

    public const int PlayerMask = 1 << Player;
    public const int MonsterMask = 1 << Monster;
    public const int WallMask = 1 << Wall;
}
