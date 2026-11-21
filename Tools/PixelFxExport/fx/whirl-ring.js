// 광폭 회전(AxeWhirl)용 프로젝트 전용 이펙트: 위에서 본 도끼 회오리. 굵은 도끼날 초승달 두 개가
// 몸 둘레를 두 바퀴 돌며 불씨 섞인 잔상 호를 남기고, 반경 끝에 바람 고리와 흙먼지가 소용돌이친다.
// 마지막 바퀴가 끝나면 둘레가 붉게 번쩍이고 불씨가 바깥으로 흩어진다.
// 회전 베기(spin-slash)의 옆에서 본 얇은 고리와 달리 위에서 본 두꺼운 두 날과 불씨 소용돌이다.
// 가운데(0, 0)가 캐릭터 발밑이고, export.mjs가 "local/whirl-ring"으로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, ease, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Whirl ring",
  desc: "Top-down berserker whirl: two heavy axe crescents sweep twice round the body leaving ember-flecked smears, a ring of wind and dust swirls at the edge, then the rim flares red and embers fly outward.",
  tags: ["melee", "axe", "spin", "whirlwind", "top-down", "one-shot"],
  size: [180, 180],
  origin: [90, 90],
  duration: 0.8,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a0a06", "#4a160c", "#8a2a12", "#d0501c", "#ff8a2a", "#ffc860", "#fff2c8"], radius: 76, seed: 9 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 7);
  const R = o.radius;
  const S = o.seed;
  const spin = seg(t, 0, 0.55);
  // 두 바퀴, 처음엔 빠르고 끝에 늦춘다
  const turn = ease.outQuad(spin) * TAU * 2;
  const fadeAll = 1 - seg(t, 0.55, 0.8);

  // 1) 바람 고리: 반경 끝을 도는 끊긴 호
  if (fadeAll > 0) {
    for (let j = 0; j < 3; j++) {
      const a0 = turn * 1.2 + j * TAU / 3;
      p.arc(0, 0, R * 0.98, 2, a0, a0 + 1.1, C[2], fadeAll * 0.9);
    }
  }

  // 2) 잔상 호와 도끼날: 두 날이 반대편에서 함께 돈다
  if (spin < 1) {
    for (let b = 0; b < 2; b++) {
      const lead = turn + b * Math.PI;
      // 잔상: 날 뒤 2라디안에 걸쳐 굵기가 줄며 어두워진다
      p.arc(0, 0, R * 0.92, (u) => 2 + 12 * u, lead - 2.2, lead, (u) => {
        if (u > 0.85) {
          return C[6];
        }
        if (u > 0.6) {
          return C[5];
        }
        if (u > 0.3) {
          return C[4];
        }
        return C[3];
      }, (u) => 0.4 + 0.6 * u);
      // 도끼날 머리: 반달 모양 쇳빛 날
      const hx = Math.cos(lead) * R * 0.78;
      const hy = Math.sin(lead) * R * 0.78;
      p.circle(hx, hy, 7, C[1]);
      p.circle(hx, hy, 6, WHITE);
      p.circle(hx - Math.cos(lead) * 3, hy - Math.sin(lead) * 3, 5, C[5]);
      p.line(0, 0, hx * 0.8, hy * 0.8, C[1]);
      p.glow(hx, hy, 16, C[4], 0.5);
    }
  }

  // 3) 소용돌이 흙먼지와 불씨: 반경 안쪽을 돌며 바깥으로 밀려난다
  for (let i = 0; i < 40; i++) {
    const g = rng(S, i, 3);
    const a0 = g.range(0, TAU);
    const r0 = R * g.range(0.35, 0.9);
    const born = g.range(0, 0.4);
    const k = seg(t, born, born + 0.4);
    if (k > 0 && k < 1) {
      const a = a0 + k * 3;
      const r = r0 + k * 12;
      let c = C[4];
      if (i % 3 === 0) {
        c = C[6];
      }
      if (i % 4 === 1) {
        c = C[2];
      }
      p.dot(Math.cos(a) * r, Math.sin(a) * r, c, 1 - k);
    }
  }

  // 4) 마무리: 둘레가 번쩍이고 불씨가 바깥으로 튄다
  const end = seg(t, 0.5, 0.8);
  if (end > 0 && end < 1) {
    p.ring(0, 0, R * (0.9 + 0.12 * end), 2, C[4], 1 - end);
    p.glowRing(0, 0, R * 0.95, 10, C[3], 0.5 * (1 - end));
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * TAU + 0.2;
      const d = R * (0.9 + end * 0.3);
      const x = Math.cos(a) * d;
      const y = Math.sin(a) * d;
      p.line(x, y, x - Math.cos(a) * 4, y - Math.sin(a) * 4, C[5], 1 - end);
    }
  }
}
