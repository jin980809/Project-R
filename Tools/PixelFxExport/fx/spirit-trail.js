// 혼령 질주(WandSpirit) 돌진 길용 프로젝트 전용 이펙트: 출발 자리(0, 0)에서 오른쪽으로 length 픽셀.
// 검보랏빛 그림자 불꽃이 길을 따라 혀처럼 날름거리며 타오르고, 해골 얼굴이 언뜻 비친 연기 덩이가
// 뒤로 흩어지며 빛나는 혼 티끌이 떠오른다. 게임은 이 그림을 돌진 거리만큼 깐다(SkillVfx.trail).
// export.mjs가 "local/spirit-trail"로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Spirit trail",
  desc: "A shadow dash: black-violet tongues of shade-fire lick up along the path, smoky puffs with fleeting skull faces peel off behind, and pale soul motes rise and fade.",
  tags: ["shadow", "dash", "trail", "spirit", "directional", "one-shot"],
  size: [168, 64],
  origin: [12, 36],
  duration: 0.75,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#08040e", "#1c0c2e", "#3a1a5c", "#6a34a0", "#a46ae0", "#dcc4ff"], length: 128, seed: 2 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 6);
  const L = o.length;
  const S = o.seed;
  const head = L * ease.outCubic(seg(t, 0, 0.28));
  const fade = 1 - seg(t, 0.35, 0.75);

  // 1) 바닥 그림자 띠
  if (fade > 0 && head > 2) {
    p.ellipse(head / 2, 2, head / 2, 3, C[1], fade);
  }

  // 2) 그림자 불꽃: 길을 따라 늘어선 혀 모양 불길(아래가 검고 끝이 보랏빛)
  for (let i = 0; i < 18; i++) {
    const g = rng(S, i, 5);
    const x = (i + 0.5) * L / 18;
    if (x < head) {
      const born = 0.28 * (x / L);
      const k = seg(t, born, born + 0.4);
      if (k < 1) {
        const h = (6 + g.range(4, 12)) * Math.sin(Math.min(1, k * 2.2) * Math.PI / 2) * (1 - k * 0.6);
        const sway = Math.sin(t * 30 + i) * 2;
        const a = 1 - seg(k, 0.5, 1);
        p.tri(x - 3, 2, x + 3, 2, x + sway, 2 - h, C[2], a);
        p.tri(x - 1.5, 2, x + 1.5, 2, x + sway * 0.7, 2 - h * 0.65, C[3], a);
        p.px(x + sway, 1 - h, C[4]);
      }
    }
  }

  // 3) 해골 연기: 연기 덩이 세 개가 뒤로 흩어지며 얼굴(눈 구멍 두 개, 입)이 잠깐 비친다
  for (let i = 0; i < 3; i++) {
    const x0 = L * (0.25 + i * 0.27);
    const born = 0.28 * (x0 / L) + 0.05;
    const k = seg(t, born, born + 0.5);
    if (k > 0 && k < 1) {
      const x = x0 - k * 14;
      const y = -6 - k * 10;
      const r = 5 + k * 3;
      const a = 1 - seg(k, 0.5, 1);
      p.circle(x, y, r + 1, C[1], a);
      p.circle(x, y, r, C[2], a);
      if (k < 0.7) {
        p.px(x - 2, y - 1, C[0]);
        p.px(x + 2, y - 1, C[0]);
        p.px(x - 2, y, C[0]);
        p.px(x + 2, y, C[0]);
        p.px(x, y + 3, C[0]);
        p.px(x - 2, y - 2, C[4]);
      }
    }
  }

  // 4) 떠오르는 혼 티끌
  for (let i = 0; i < 20; i++) {
    const g = rng(S, i, 8);
    const x0 = g.range(0, L);
    const born = 0.28 * (x0 / L);
    const k = seg(t, born, born + g.range(0.35, 0.55));
    if (k > 0 && k < 1) {
      p.dot(x0 - k * 6 + Math.sin(k * 8 + i) * 2, -k * 22 + g.range(-3, 3), C[4 + (i % 2)], 1 - k);
    }
  }
  if (t < 0.32) {
    p.glow(head, -2, 14, C[4], 0.5 * (1 - seg(t, 0.2, 0.32)));
  }
}
