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
public class SkillFxPlayer : MonoBehaviour
{
    private const float FadeTime = 0.3f;
    private const string GradientShaderName = "Tower/SkillFxGradient";

    private struct Slot
    {
        public bool active;
        public int clip;
        public float startTime;
        public float endTime;
        public bool looping;
        public Transform follow;
        public Color color;
    }

    [SerializeField] private SkillFxClip[] clips;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private int groundOrder = -800;
    [SerializeField] private int airOrder = 3000;

    private Slot[] slots;
    private Material gradientMaterial;

    private void Awake()
    {
        slots = new Slot[renderers.Length];
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
        int slot = Begin(fx, color, duration, follow);
        if (slot >= 0)
        {
            SpriteRenderer target = renderers[slot];
            float size = clips[(int)fx].frames[0].bounds.size.x;
            float scale = radius * 2f / size;
            target.transform.position = position;
            target.transform.rotation = Quaternion.identity;
            target.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // 방향이 있는 이펙트(돌진 자국): 가로로 길게 늘려 direction 쪽으로 돌린다.
    public void PlayDirected(SkillFx fx, Vector2 position, float halfLength, Vector2 direction, Color color)
    {
        int slot = Begin(fx, color, 0f, null);
        if (slot >= 0)
        {
            SpriteRenderer target = renderers[slot];
            Vector2 size = clips[(int)fx].frames[0].bounds.size;
            target.transform.position = position;
            target.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            target.transform.localScale = new Vector3(halfLength * 2f / size.x, 1f, 1f);
        }
    }

    // 빈 칸(없으면 가장 오래된 칸)을 잡아 재생을 시작한다. 프레임이 없는 종류면 -1.
    private int Begin(SkillFx fx, Color color, float duration, Transform follow)
    {
        int index = (int)fx;
        int pick = -1;
        bool valid = fx != SkillFx.None && index < clips.Length && clips[index].frames != null && clips[index].frames.Length > 0;
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

            SkillFxClip clip = clips[index];
            Slot slot = new Slot();
            slot.active = true;
            slot.clip = index;
            slot.startTime = Time.time;
            slot.looping = duration > 0f;
            slot.endTime = Time.time + clip.frames.Length / clip.frameRate;
            if (slot.looping)
            {
                slot.endTime = Time.time + duration;
            }
            slot.follow = follow;
            slot.color = color;
            slots[pick] = slot;

            SpriteRenderer target = renderers[pick];
            target.sprite = clip.frames[0];
            target.color = color;
            if (clip.ground)
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
                    target.enabled = false;
                }
                else
                {
                    SkillFxClip clip = clips[slots[i].clip];
                    int frame = (int)((now - slots[i].startTime) * clip.frameRate);
                    if (slots[i].looping)
                    {
                        frame %= clip.frames.Length;
                        Color color = slots[i].color;
                        color.a *= Mathf.Clamp01((slots[i].endTime - now) / FadeTime);
                        target.color = color;
                    }
                    else
                    {
                        // 한 번 재생은 뒤쪽 40% 동안 흐려진다.
                        frame = Mathf.Min(frame, clip.frames.Length - 1);
                        float length = clip.frames.Length / clip.frameRate;
                        Color color = slots[i].color;
                        color.a *= Mathf.Clamp01((slots[i].endTime - now) / (length * 0.4f));
                        target.color = color;
                    }
                    target.sprite = clip.frames[frame];
                    if (slots[i].follow != null)
                    {
                        target.transform.position = slots[i].follow.position;
                    }
                }
            }
        }
    }
}
