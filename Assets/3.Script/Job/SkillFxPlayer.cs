using System;
using UnityEngine;

// 스킬 이펙트 하나의 프레임 묶음(SkillFx 순서). 프레임은 정사각형이고 그림의 원이 칸을 꽉 채운다.
[Serializable]
public class SkillFxClip
{
    public Sprite[] frames;
    public float frameRate = 18f;
    [Tooltip("true면 바닥에 깔리는 이펙트(캐릭터 뒤에 그린다)")]
    public bool ground;
}

// 스킬 이펙트를 미리 만들어 둔 SpriteRenderer 풀로 재생한다(할당 없음).
// 이펙트 그림은 명암만 담은 회색 그림이고, Resources/SkillFxGradient 셰이더가 스킬 색으로 외곽선·면·하이라이트를 칠한다.
// 셰이더를 못 찾으면 기본 스프라이트 재질(색 곱하기)로 그대로 보인다.
// - 한 번 재생: 프레임을 끝까지 돌고 사라진다.
// - 지속 재생(장판, 강화 기운): 지속 시간 동안 프레임을 반복하고 마지막에 흐려진다.
//   장판에 나타나는 그림·사라지는 그림이 있으면 처음에 나타나는 그림, 끝에 사라지는 그림을 한 번씩 재생한다(흐리지 않는다).
public class SkillFxPlayer : MonoBehaviour
{
    private const float FadeTime = 0.3f;
    private const string GradientShaderName = "Tower/SkillFxGradient";

    private struct Slot
    {
        public bool active;
        public Sprite[] frames;
        public float frameRate;
        // 한 번 재생의 끝을 흐리게 할지(색 그림은 그림 안에서 이미 흐려진다)
        public bool fadeOneShot;
        public float startTime;
        public float endTime;
        public bool looping;
        public Transform follow;
        public Color color;
        // 장판의 나타나는 그림·사라지는 그림(없으면 null)
        public Sprite[] introFrames;
        public float introRate;
        public Sprite[] outroFrames;
        public float outroRate;
    }

    [SerializeField] private SkillFxClip[] clips;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private int groundOrder = -800;
    [SerializeField] private int airOrder = 3000;
    [Tooltip("스킬마다 따로 그린 컬러 프레임용 재질(빛 영향 없는 스프라이트 재질). 없으면 렌더러 기본 재질.")]
    [SerializeField] private Material frameMaterial;
    [Tooltip("몬스터 폭발(폭탄 버섯 등)·보스 내려찍기 픽셀 그림(impact 프레임). 그림 픽셀 그대로(32픽셀 = 1유닛) 그린다.")]
    [SerializeField] private SkillVfx monsterExplosion = new SkillVfx();
    [SerializeField] private SkillVfx bossImpact = new SkillVfx();
    [Tooltip("큰 지도 순간이동(떠나는 자리와 도착한 자리)")]
    [SerializeField] private SkillVfx teleport = new SkillVfx();

    private Slot[] slots;
    private Material gradientMaterial;
    private Material defaultMaterial;

    private void Awake()
    {
        slots = new Slot[renderers.Length];
        defaultMaterial = frameMaterial;
        if (defaultMaterial == null && renderers.Length > 0)
        {
            defaultMaterial = renderers[0].sharedMaterial;
        }
        Shader shader = Shader.Find(GradientShaderName);
        if (shader != null)
        {
            // 모든 칸이 같은 재질 하나를 나눠 쓴다(색은 SpriteRenderer.color로 칸마다 다르게).
            gradientMaterial = new Material(shader);
        }
        else
        {
            Debug.LogWarning("[SkillFx] " + GradientShaderName + " 셰이더를 찾지 못해 기본 재질로 그립니다.");
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
            if (gradientMaterial != null)
            {
                renderers[i].sharedMaterial = gradientMaterial;
            }
        }
    }

    private void OnDestroy()
    {
        if (gradientMaterial != null)
        {
            Destroy(gradientMaterial);
        }
    }

    public void Play(SkillFx fx, Vector2 position, float radius, Color color, float duration, Transform follow)
    {
        Play(fx, position, radius, color, duration, follow, 0f);
    }

    // angle(도): 오른쪽을 보고 그린 방향성 이펙트(십자 베기, 휩쓸기, 내려치기)를 조준 쪽으로 돌린다.
    // 왼쪽을 볼 때는 위아래를 뒤집어 그림이 거꾸로 서지 않게 한다.
    public void Play(SkillFx fx, Vector2 position, float radius, Color color, float duration, Transform follow, float angle)
    {
        int index = (int)fx;
        if (fx != SkillFx.None && index < clips.Length)
        {
            SkillFxClip clip = clips[index];
            Place(Begin(clip.frames, clip.frameRate, clip.ground, color, duration, follow, gradientMaterial), position, radius, angle, true, false);
        }
    }

    // 스킬마다 따로 그린 컬러 프레임을 재생한다. 그림 칸 가로가 지름(radius * 2)이 된다.
    // directional이면 오른쪽을 보고 그린 그림을 angle(도)로 돌린다.
    public void PlayFrames(Sprite[] frames, float frameRate, bool ground, Vector2 position, float radius, float duration, Transform follow, float angle, bool directional)
    {
        PlayFrames(frames, frameRate, ground, position, radius, duration, follow, angle, directional, false);
    }

    // flipVertical: 바라보는 쪽 기준으로 위아래를 뒤집는다. 위에서 아래로 그린 베기 그림을 아래에서 위로 올려 벨 때 쓴다.
    public void PlayFrames(Sprite[] frames, float frameRate, bool ground, Vector2 position, float radius, float duration, Transform follow, float angle, bool directional, bool flipVertical)
    {
        float useAngle = 0f;
        if (directional)
        {
            useAngle = angle;
        }
        Place(Begin(frames, frameRate, ground, Color.white, duration, follow, defaultMaterial), position, radius, useAngle, directional, flipVertical);
    }

    // 장판: 나타나는 그림 → 반복 그림 → 사라지는 그림 순서로 지속 시간(duration) 동안 재생한다. 바닥 장판은 돌리지 않는다.
    public void PlayZone(SkillVfx vfx, Vector2 position, float radius, float duration)
    {
        int slot = Begin(vfx.cast, vfx.castFps, vfx.castGround, Color.white, duration, null, defaultMaterial);
        if (slot >= 0)
        {
            if (vfx.Has(vfx.castIntro) && vfx.castIntroFps > 0f)
            {
                slots[slot].introFrames = vfx.castIntro;
                slots[slot].introRate = vfx.castIntroFps;
                renderers[slot].sprite = vfx.castIntro[0];
            }
            if (vfx.Has(vfx.castOutro) && vfx.castOutroFps > 0f)
            {
                slots[slot].outroFrames = vfx.castOutro;
                slots[slot].outroRate = vfx.castOutroFps;
            }
            Place(slot, position, radius, 0f, false, false);
        }
    }

    // 몬스터 폭발 그림을 띄운다. 그림이 없으면 false(예전 이펙트로 대신 그린다).
    public bool PlayMonsterExplosion(Vector2 position)
    {
        return PlayNative(monsterExplosion, position);
    }

    public bool PlayBossImpact(Vector2 position)
    {
        return PlayNative(bossImpact, position);
    }

    public bool PlayTeleport(Vector2 position)
    {
        return PlayNative(teleport, position);
    }

    private bool PlayNative(SkillVfx vfx, Vector2 position)
    {
        bool has = vfx.Has(vfx.impact);
        if (has)
        {
            // 그림 가로 픽셀 / 32 = 지름(유닛)
            float radius = vfx.impact[0].rect.width / 64f;
            PlayFrames(vfx.impact, vfx.impactFps, vfx.impactGround, position, radius, 0f, null, 0f, false);
        }
        return has;
    }

    // mirrorOnly면 조준 각도로 돌리지 않고, 왼쪽을 볼 때만 좌우를 뒤집는다(옆에서 본 바닥 그림).
    // 180도로 돌린 뒤 위아래를 뒤집으면 좌우 반전이 된다.
    public void PlayFacing(Sprite[] frames, float frameRate, bool ground, Vector2 position, float radius, float duration, Transform follow, float angle, bool directional, bool mirrorOnly)
    {
        float useAngle = angle;
        if (mirrorOnly)
        {
            useAngle = 0f;
            if (angle > 90f || angle < -90f)
            {
                useAngle = 180f;
            }
        }
        PlayFrames(frames, frameRate, ground, position, radius, duration, follow, useAngle, directional || mirrorOnly);
    }

    // 가로로 길게 늘린 컬러 프레임(돌진 길). width는 세로 폭(유닛). 왼쪽을 향하면 위아래를 뒤집는다.
    public void PlayFramesDirected(Sprite[] frames, float frameRate, Vector2 position, float halfLength, float width, Vector2 direction)
    {
        int slot = Begin(frames, frameRate, true, Color.white, 0f, null, defaultMaterial);
        if (slot >= 0)
        {
            SpriteRenderer target = renderers[slot];
            Vector2 size = frames[0].bounds.size;
            float flip = 1f;
            if (direction.x < 0f)
            {
                flip = -1f;
            }
            target.transform.position = position;
            target.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            target.transform.localScale = new Vector3(halfLength * 2f / size.x, width / size.y * flip, 1f);
        }
    }

    private void Place(int slot, Vector2 position, float radius, float angle, bool flipLeft, bool flipVertical)
    {
        if (slot >= 0)
        {
            SpriteRenderer target = renderers[slot];
            float size = slots[slot].frames[0].bounds.size.x;
            float scale = radius * 2f / size;
            float flip = 1f;
            if (flipLeft && (angle > 90f || angle < -90f))
            {
                flip = -1f;
            }
            if (flipVertical)
            {
                flip = -flip;
            }
            target.transform.position = position;
            target.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            target.transform.localScale = new Vector3(scale, scale * flip, 1f);
        }
    }

    // 조준 방향으로 돌려 그리는 종류인지
    public bool IsDirectional(SkillFx fx)
    {
        return fx == SkillFx.CrossSlash || fx == SkillFx.Sweep || fx == SkillFx.Cleave;
    }

    // 방향이 있는 이펙트(돌진 자국): 가로로 길게 늘려 direction 쪽으로 돌린다.
    public void PlayDirected(SkillFx fx, Vector2 position, float halfLength, Vector2 direction, Color color)
    {
        int index = (int)fx;
        if (fx != SkillFx.None && index < clips.Length)
        {
            SkillFxClip clip = clips[index];
            int slot = Begin(clip.frames, clip.frameRate, clip.ground, color, 0f, null, gradientMaterial);
            if (slot >= 0)
            {
                SpriteRenderer target = renderers[slot];
                Vector2 size = clip.frames[0].bounds.size;
                target.transform.position = position;
                target.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                target.transform.localScale = new Vector3(halfLength * 2f / size.x, 1f, 1f);
            }
        }
    }

    // 지속 시간이 있는 반복 재생(장판)을 모두 끈다. 방을 옮기면 FloorDirector가 부른다.
    public void StopLooping()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].active && slots[i].looping)
            {
                slots[i].active = false;
                slots[i].follow = null;
                slots[i].frames = null;
                renderers[i].enabled = false;
            }
        }
    }

    // 방을 옮기면 FloorDirector가 부른다. 남은 이펙트를 모두 끈다(플레이어를 따라다니는 강화 기운은 남긴다).
    public void StopAll()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].active)
            {
                bool keep = slots[i].follow != null && slots[i].follow.GetComponent<PlayerController>() != null;
                if (!keep)
                {
                    slots[i].active = false;
                    slots[i].follow = null;
                    slots[i].frames = null;
                    renderers[i].enabled = false;
                }
            }
        }
    }

    // 빈 칸(없으면 가장 오래된 칸)을 잡아 재생을 시작한다. 프레임이 없는 종류면 -1.
    private int Begin(Sprite[] frames, float frameRate, bool ground, Color color, float duration, Transform follow, Material material)
    {
        int pick = -1;
        bool valid = frames != null && frames.Length > 0 && frameRate > 0f;
        if (valid)
        {
            float oldest = float.MaxValue;
            for (int i = 0; i < slots.Length && pick < 0; i++)
            {
                if (!slots[i].active)
                {
                    pick = i;
                }
                else if (slots[i].startTime < oldest)
                {
                    oldest = slots[i].startTime;
                }
            }
            if (pick < 0)
            {
                for (int i = 0; i < slots.Length && pick < 0; i++)
                {
                    if (slots[i].startTime <= oldest)
                    {
                        pick = i;
                    }
                }
            }

            Slot slot = new Slot();
            slot.active = true;
            slot.frames = frames;
            slot.frameRate = frameRate;
            slot.startTime = Time.time;
            slot.looping = duration > 0f;
            slot.endTime = Time.time + frames.Length / frameRate;
            if (slot.looping)
            {
                slot.endTime = Time.time + duration;
            }
            slot.follow = follow;
            slot.color = color;
            slot.fadeOneShot = material == null || material == gradientMaterial;
            slots[pick] = slot;

            SpriteRenderer target = renderers[pick];
            if (material != null)
            {
                target.sharedMaterial = material;
            }
            target.sprite = frames[0];
            target.color = color;
            if (ground)
            {
                target.sortingOrder = groundOrder;
            }
            else
            {
                target.sortingOrder = airOrder;
            }
            target.enabled = true;
        }
        return pick;
    }

    // 지속 재생 칸의 지금 그림: 앞쪽은 나타나는 그림, 끝쪽은 사라지는 그림, 그 사이는 반복 그림.
    private Sprite ZoneFrame(int index, float now)
    {
        Slot slot = slots[index];
        float elapsed = now - slot.startTime;
        float introLength = 0f;
        if (slot.introFrames != null)
        {
            introLength = slot.introFrames.Length / slot.introRate;
        }
        float outroStart = slot.endTime;
        if (slot.outroFrames != null)
        {
            outroStart = slot.endTime - slot.outroFrames.Length / slot.outroRate;
        }
        Sprite sprite;
        if (now >= outroStart)
        {
            int frame = Mathf.Min((int)((now - outroStart) * slot.outroRate), slot.outroFrames.Length - 1);
            sprite = slot.outroFrames[frame];
        }
        else if (elapsed < introLength)
        {
            int frame = Mathf.Min((int)(elapsed * slot.introRate), slot.introFrames.Length - 1);
            sprite = slot.introFrames[frame];
        }
        else
        {
            int frame = (int)((elapsed - introLength) * slot.frameRate) % slot.frames.Length;
            sprite = slot.frames[frame];
        }
        return sprite;
    }

    private void Update()
    {
        float now = Time.time;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].active)
            {
                SpriteRenderer target = renderers[i];
                if (now >= slots[i].endTime)
                {
                    slots[i].active = false;
                    slots[i].follow = null;
                    slots[i].frames = null;
                    target.enabled = false;
                }
                else
                {
                    Sprite[] frames = slots[i].frames;
                    int frame = (int)((now - slots[i].startTime) * slots[i].frameRate);
                    if (slots[i].looping)
                    {
                        frame %= frames.Length;
                        // 사라지는 그림이 있으면 그림이 알아서 사라지므로 흐리지 않는다.
                        if (slots[i].outroFrames == null)
                        {
                            Color color = slots[i].color;
                            color.a *= Mathf.Clamp01((slots[i].endTime - now) / FadeTime);
                            target.color = color;
                        }
                    }
                    else
                    {
                        // 한 번 재생은 뒤쪽 40% 동안 흐려진다.
                        frame = Mathf.Min(frame, frames.Length - 1);
                        float length = frames.Length / slots[i].frameRate;
                        if (slots[i].fadeOneShot)
                        {
                            Color color = slots[i].color;
                            color.a *= Mathf.Clamp01((slots[i].endTime - now) / (length * 0.4f));
                            target.color = color;
                        }
                    }
                    target.sprite = frames[frame];
                    if (slots[i].looping)
                    {
                        target.sprite = ZoneFrame(i, now);
                    }
                    if (slots[i].follow != null)
                    {
                        target.transform.position = slots[i].follow.position;
                    }
                }
            }
        }
    }
}
