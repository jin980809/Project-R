using System;
using UnityEngine;

// 효과음 종류. 순서를 바꾸면 SoundLibrary 에셋의 칸이 어긋나므로 새 항목은 끝(Count 앞)에 추가한다.
public enum SoundId : byte
{
    None,
    // UI
    UiClick,
    UiOpen,
    UiClose,
    UiError,
    // 플레이어 공격
    SwordSwing,
    HeavySwing,
    SpearThrust,
    BowRelease,
    CrossbowShot,
    GunShot,
    MagicShot,
    BowCharge,
    // 플레이어 동작
    Roll,
    Block,
    Parry,
    PlayerHurt,
    PlayerDie,
    Revive,
    // 몬스터
    MonsterHit,
    CriticalHit,
    MonsterDie,
    MonsterAttack,
    MonsterShoot,
    BossRoar,
    // 스킬
    SkillCast,
    SkillSlash,
    SkillDash,
    SkillFire,
    SkillIce,
    SkillLightning,
    SkillHoly,
    SkillDark,
    SkillBuff,
    Heal,
    Explosion,
    // 아이템, 진행
    Pickup,
    Coin,
    PotionUse,
    LevelUp,
    NodeUnlock,
    Upgrade,
    DoorOpen,
    DoorLock,
    RoomClear,
    ChestOpen,
    StairsOpen,
    JobChange,
    Count
}

// 배경음악 종류
public enum MusicId : byte
{
    None,
    Title,
    Lobby,
    Combat,
    Boss,
    Victory,
    Defeat,
    Count
}

// 효과음·배경음악 목록. 이 에셋(Assets/2.Model/Data/SoundLibrary.asset)의 칸에 AudioClip을 넣기만 하면 게임에서 재생된다.
// 클립이 비어 있는 칸은 아무 소리도 내지 않는다. 초기 구성을 다시 해도 넣어 둔 클립은 지우지 않는다.
[CreateAssetMenu(menuName = "Tower/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    [Serializable]
    public class SoundEntry
    {
        public SoundId id;
        [Tooltip("여러 개를 넣으면 무작위로 하나를 고른다(같은 소리 반복을 줄인다).")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생할 때마다 음높이를 이 범위에서 흔든다(0.05 = ±5%).")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
        [Tooltip("같은 소리를 이 시간 안에 다시 재생하지 않는다(동시에 여러 마리를 칠 때 소리가 겹쳐 커지는 것을 막는다).")]
        public float minInterval = 0.04f;
    }

    [Serializable]
    public class MusicEntry
    {
        public MusicId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private SoundEntry[] sounds = new SoundEntry[0];
    [SerializeField] private MusicEntry[] music = new MusicEntry[0];

    public int SoundCount => sounds.Length;
    public int MusicCount => music.Length;

    public SoundEntry GetSoundAt(int index)
    {
        return sounds[index];
    }

    public MusicEntry GetMusicAt(int index)
    {
        return music[index];
    }

    // 초기 구성 도구가 부른다. 빠진 종류만 칸을 추가하고, 이미 있는 칸(넣어 둔 클립)은 그대로 둔다.
    public void EnsureAllEntries()
    {
        int soundTotal = (int)SoundId.Count;
        SoundEntry[] newSounds = new SoundEntry[soundTotal - 1];
        for (int i = 1; i < soundTotal; i++)
        {
            SoundEntry found = null;
            for (int j = 0; j < sounds.Length && found == null; j++)
            {
                if (sounds[j] != null && (int)sounds[j].id == i)
                {
                    found = sounds[j];
                }
            }
            if (found == null)
            {
                found = new SoundEntry();
                found.id = (SoundId)i;
                found.clips = new AudioClip[0];
            }
            newSounds[i - 1] = found;
        }
        sounds = newSounds;

        int musicTotal = (int)MusicId.Count;
        MusicEntry[] newMusic = new MusicEntry[musicTotal - 1];
        for (int i = 1; i < musicTotal; i++)
        {
            MusicEntry found = null;
            for (int j = 0; j < music.Length && found == null; j++)
            {
                if (music[j] != null && (int)music[j].id == i)
                {
                    found = music[j];
                }
            }
            if (found == null)
            {
                found = new MusicEntry();
                found.id = (MusicId)i;
            }
            newMusic[i - 1] = found;
        }
        music = newMusic;
    }
}
