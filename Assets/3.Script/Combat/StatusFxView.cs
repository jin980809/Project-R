using System;
using UnityEngine;

// 캐릭터·몬스터에 걸린 상태이상과 강화를 몸 위에 그린다(클라이언트 연출만).
// 종류마다 프레임 애니메이션 한 겹과 (있으면) 파티클 하나. 켜고 끌 때만 바뀌고 매 프레임 할당이 없다.
// 상태 비트는 PlayerController와 같다: 1 화상, 2 출혈, 4 둔화, 8 약화, 16 기절.
// 강화: 0 공격(불꽃 기운), 1 방어(황금 막), 2 속도(발밑 바람), 3 치명(머리 위 조준선), -1 없음.
public class StatusFxView : MonoBehaviour
{
    public const int StatusCount = 6;
    public const int BuffCount = 4;

    [Serializable]
    public class Layer
    {
        public SpriteRenderer renderer;
        public Sprite[] frames;
        public float frameRate = 10f;
        public ParticleSystem particles;
        [Tooltip("몸 높이에 대한 위치 비율(0 발밑, 1 머리 위)")]
        public float heightRatio;
        [Tooltip("몸 높이에 더하는 위치(유닛)")]
        public float heightOffset;
    }

    [Tooltip("화상, 출혈, 둔화, 약화, 기절, 중독 순서")]
    [SerializeField] private Layer[] statusLayers = new Layer[StatusCount];
    [Tooltip("공격, 방어, 속도, 치명 순서")]
    [SerializeField] private Layer[] buffLayers = new Layer[BuffCount];
    [Tooltip("그림을 그린 기준 몸 높이(유닛). 이보다 큰 몸이면 그만큼 키운다.")]
    [SerializeField] private float referenceHeight = 1.2f;

    private byte shownStatus;
    private int shownBuff = -1;
    private float bodyHeight = 1.2f;
    private float time;

    private void Awake()
    {
        for (int i = 0; i < statusLayers.Length; i++)
        {
            SetLayer(statusLayers[i], false);
        }
        for (int i = 0; i < buffLayers.Length; i++)
        {
            SetLayer(buffLayers[i], false);
        }
    }

    // 몸 높이(유닛, 발밑 기준 로컬). 크기와 위치를 맞춘다.
    public void SetBodyHeight(float height)
    {
        if (Mathf.Abs(height - bodyHeight) > 0.01f)
        {
            bodyHeight = height;
            float scale = Mathf.Clamp(height / referenceHeight, 0.6f, 2.5f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    public void SetState(byte statusMask, int buff)
    {
        if (statusMask != shownStatus)
        {
            for (int i = 0; i < statusLayers.Length; i++)
            {
                bool was = (shownStatus & (1 << i)) != 0;
                bool now = (statusMask & (1 << i)) != 0;
                if (was != now)
                {
                    SetLayer(statusLayers[i], now);
                }
            }
            shownStatus = statusMask;
        }
        if (buff != shownBuff)
        {
            if (shownBuff >= 0 && shownBuff < buffLayers.Length)
            {
                SetLayer(buffLayers[shownBuff], false);
            }
            if (buff >= 0 && buff < buffLayers.Length)
            {
                SetLayer(buffLayers[buff], true);
            }
            shownBuff = buff;
        }
    }

    private void SetLayer(Layer layer, bool on)
    {
        if (layer == null)
        {
            return;
        }
        if (layer.renderer != null)
        {
            layer.renderer.enabled = on;
            // 위치는 높이 비율로(스케일은 부모가 맡으므로 기준 높이로 계산)
            layer.renderer.transform.localPosition = new Vector3(0f, referenceHeight * layer.heightRatio + layer.heightOffset, 0f);
        }
        if (layer.particles != null)
        {
            if (on)
            {
                layer.particles.Play(true);
            }
            else
            {
                layer.particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void LateUpdate()
    {
        if (shownStatus == 0 && shownBuff < 0)
        {
            return;
        }
        time += Time.deltaTime;
        for (int i = 0; i < statusLayers.Length; i++)
        {
            if ((shownStatus & (1 << i)) != 0)
            {
                Animate(statusLayers[i], i * 0.13f);
            }
        }
        if (shownBuff >= 0 && shownBuff < buffLayers.Length)
        {
            Animate(buffLayers[shownBuff], 0f);
        }
    }

    private void Animate(Layer layer, float offset)
    {
        if (layer.renderer != null && layer.frames != null && layer.frames.Length > 0)
        {
            int frame = (int)((time + offset) * layer.frameRate) % layer.frames.Length;
            layer.renderer.sprite = layer.frames[frame];
        }
    }
}
