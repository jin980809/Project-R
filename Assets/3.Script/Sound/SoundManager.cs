using UnityEngine;

// 효과음과 배경음악 재생. 씬마다 하나 있고 GameHub.Sound로 접근한다.
// - 효과음은 미리 만든 AudioSource 묶음을 돌려 가며 쓴다(재생 중 할당 없음).
// - 클립이 없는 소리는 조용히 넘어간다. SoundLibrary 에셋에 클립을 넣으면 바로 들린다.
// - 음량은 MetaProgress의 설정(전체, 효과음, 배경음)을 따른다.
// GameHub.Awake가 음악·음량을 바로 정하므로 그보다 먼저 준비되어야 한다.
[DefaultExecutionOrder(-100)]
public class SoundManager : MonoBehaviour
{
    private const int ChannelMaster = 0;
    private const int ChannelEffects = 1;
    private const int ChannelMusic = 2;

    [SerializeField] private SoundLibrary library;
    [SerializeField] private int voiceCount = 16;
    [Tooltip("화면 가장자리 쪽 소리를 좌우로 얼마나 치우치게 할지(0이면 끔)")]
    [SerializeField] private float panStrength = 0.6f;
    [SerializeField] private float musicFadeTime = 0.8f;

    private AudioSource[] voices;
    private AudioSource musicSource;
    private int nextVoice;
    private SoundLibrary.SoundEntry[] soundLookup;
    private SoundLibrary.MusicEntry[] musicLookup;
    private readonly float[] lastPlayTime = new float[(int)SoundId.Count];
    private float effectsVolume = 1f;
    private float musicVolume = 1f;
    private MusicId currentMusic = MusicId.None;
    private float currentMusicEntryVolume = 1f;
    private float musicFade = 1f;
    private MusicId pendingMusic = MusicId.None;
    private bool musicSwitching;
    private Camera listenerCamera;

    private void Awake()
    {
        soundLookup = new SoundLibrary.SoundEntry[(int)SoundId.Count];
        musicLookup = new SoundLibrary.MusicEntry[(int)MusicId.Count];
        if (library != null)
        {
            for (int i = 0; i < library.SoundCount; i++)
            {
                SoundLibrary.SoundEntry entry = library.GetSoundAt(i);
                if (entry != null && (int)entry.id < soundLookup.Length)
                {
                    soundLookup[(int)entry.id] = entry;
                }
            }
            for (int i = 0; i < library.MusicCount; i++)
            {
                SoundLibrary.MusicEntry entry = library.GetMusicAt(i);
                if (entry != null && (int)entry.id < musicLookup.Length)
                {
                    musicLookup[(int)entry.id] = entry;
                }
            }
        }

        voices = new AudioSource[voiceCount];
        for (int i = 0; i < voiceCount; i++)
        {
            voices[i] = CreateSource("Voice" + i);
        }
        musicSource = CreateSource("Music");
        musicSource.loop = true;
        for (int i = 0; i < lastPlayTime.Length; i++)
        {
            lastPlayTime[i] = -10f;
        }
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    public void SetCamera(Camera camera)
    {
        listenerCamera = camera;
    }

    // 설정이 바뀌면 GameHub.ApplySettings가 부른다.
    public void ApplyVolumes(MetaProgress meta)
    {
        float master = meta.GetVolume01(ChannelMaster);
        effectsVolume = master * meta.GetVolume01(ChannelEffects);
        musicVolume = master * meta.GetVolume01(ChannelMusic);
        UpdateMusicVolume();
    }

    // 화면 가운데에서 들리는 소리(UI 등)
    public void Play(SoundId id)
    {
        PlayInternal(id, 1f, 0f);
    }

    // 월드 위치에서 난 소리. 화면 좌우에 따라 살짝 치우친다.
    public void PlayAt(SoundId id, Vector2 position)
    {
        PlayAt(id, position, 1f);
    }

    public void PlayAt(SoundId id, Vector2 position, float volumeScale)
    {
        float pan = 0f;
        if (listenerCamera != null && panStrength > 0f)
        {
            float halfWidth = listenerCamera.orthographicSize * listenerCamera.aspect;
            if (halfWidth > 0.01f)
            {
                float offset = (position.x - listenerCamera.transform.position.x) / halfWidth;
                pan = Mathf.Clamp(offset, -1f, 1f) * panStrength;
            }
        }
        PlayInternal(id, volumeScale, pan);
    }

    private void PlayInternal(SoundId id, float volumeScale, float pan)
    {
        int index = (int)id;
        if (index > 0 && index < soundLookup.Length)
        {
            SoundLibrary.SoundEntry entry = soundLookup[index];
            bool hasClip = entry != null && entry.clips != null && entry.clips.Length > 0;
            float now = Time.unscaledTime;
            if (hasClip && now - lastPlayTime[index] >= entry.minInterval && effectsVolume > 0f)
            {
                AudioClip clip = entry.clips[Random.Range(0, entry.clips.Length)];
                if (clip != null)
                {
                    lastPlayTime[index] = now;
                    AudioSource voice = voices[nextVoice];
                    nextVoice = (nextVoice + 1) % voices.Length;
                    voice.clip = clip;
                    voice.volume = entry.volume * volumeScale * effectsVolume;
                    voice.pitch = 1f + Random.Range(-entry.pitchJitter, entry.pitchJitter);
                    voice.panStereo = pan;
                    voice.Play();
                }
            }
        }
    }

    // 배경음악을 바꾼다. 같은 곡이면 그대로 두고, 다른 곡이면 잠깐 줄였다가 바꾼다.
    public void PlayMusic(MusicId id)
    {
        if (id != currentMusic || musicSwitching)
        {
            pendingMusic = id;
            musicSwitching = true;
            if (!musicSource.isPlaying)
            {
                musicFade = 0f;
            }
        }
    }

    private void Update()
    {
        if (musicSwitching)
        {
            // 줄이는 중
            musicFade -= Time.unscaledDeltaTime / musicFadeTime;
            if (musicFade <= 0f)
            {
                musicFade = 0f;
                musicSwitching = false;
                StartMusic(pendingMusic);
            }
            UpdateMusicVolume();
        }
        else if (musicFade < 1f)
        {
            // 키우는 중
            musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / musicFadeTime);
            UpdateMusicVolume();
        }
    }

    private void StartMusic(MusicId id)
    {
        currentMusic = id;
        musicSource.Stop();
        musicSource.clip = null;
        SoundLibrary.MusicEntry entry = null;
        if ((int)id < musicLookup.Length)
        {
            entry = musicLookup[(int)id];
        }
        if (entry != null && entry.clip != null)
        {
            currentMusicEntryVolume = entry.volume;
            musicSource.clip = entry.clip;
            musicSource.Play();
        }
    }

    private void UpdateMusicVolume()
    {
        if (musicSource != null)
        {
            musicSource.volume = currentMusicEntryVolume * musicVolume * musicFade;
        }
    }
}
