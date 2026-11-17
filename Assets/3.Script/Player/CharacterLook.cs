// 캐릭터 외형(커스터마이징). 네트워크로 그대로 보내는 값 형식이다.
// 파츠는 패키지 PartsManager의 번호, 색은 CharacterVisual의 팔레트 번호다.
public struct CharacterLook
{
    // 수염 없음
    public const byte NoBeard = 255;

    public byte hair;
    public byte eye;
    public byte beard;
    public byte skin;
    public byte hairColor;
    public byte skinColor;
    public byte beardColor;
    // 0이면 아직 정하지 않은 값(기본 모습으로 그린다)
    public byte valid;

    // 기기 저장용으로 두 정수에 나눠 담는다.
    public int PackParts()
    {
        return hair | (eye << 8) | (beard << 16) | (skin << 24);
    }

    public int PackColors()
    {
        return hairColor | (skinColor << 8) | (beardColor << 16) | (valid << 24);
    }

    public static CharacterLook Unpack(int parts, int colors)
    {
        CharacterLook look = new CharacterLook();
        look.hair = (byte)(parts & 0xFF);
        look.eye = (byte)((parts >> 8) & 0xFF);
        look.beard = (byte)((parts >> 16) & 0xFF);
        look.skin = (byte)((parts >> 24) & 0xFF);
        look.hairColor = (byte)(colors & 0xFF);
        look.skinColor = (byte)((colors >> 8) & 0xFF);
        look.beardColor = (byte)((colors >> 16) & 0xFF);
        look.valid = (byte)((colors >> 24) & 0xFF);
        return look;
    }

    public bool SameAs(in CharacterLook other)
    {
        return PackParts() == other.PackParts() && PackColors() == other.PackColors();
    }
}
