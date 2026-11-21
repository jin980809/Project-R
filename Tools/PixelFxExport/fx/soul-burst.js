// 영혼 폭발(WandSoul) 적중용 프로젝트 전용 이펙트: 맞은 자리에서 큰 영혼이 솟아 부풀다 터지고,
// 작은 유령들이 사방으로 빙글빙글 날아가며 흐려진다.
// 0~0.12초 보랏빛 섬광, 0.05~0.34초 큰 영혼(눈 달린 유령)이 솟아오르며 부풀어 떨림,
// 0.34초 하얗게 터지며 충격 고리, 0.34~1.6초 유령 7마리가 꼬리를 흔들며 휘어진 길로 날아가 사라진다.
// 가운데(0, 0)가 맞은 자리이고, export.mjs가 "local/soul-burst"로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, bump, ease, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Soul burst",
  desc: "A big ghost rises out of the hit point, swells and trembles, bursts in a white flash and shock ring, and a flock of little ghosts swirls out in every direction trailing wisps.",
  tags: ["shadow", "ghost", "soul", "explosion", "spirit", "one-shot"],
  size: [176, 176],
  origin: [88, 96],
  duration: 1.6,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#120818", "#2e1250", "#5a2496", "#8a44d0", "#b880ff", "#e4ccff", "#ffffff"], ghost: ["#3a2a5c", "#9c8cd0", "#dcd4ff", "#ffffff"], seed: 4 },
};

const Burst = 0.34;

// 유령 한 마리: (x, y) 머리 가운데, r 머리 반지름, dir 날아가는 방향(라디안), wave 꼬리 흔들림 위상, a 진하기
function ghost(p, G, x, y, r, dir, wave, a) {
  // 꼬리: 날아가는 반대쪽으로 가늘어지며 흔들린다
  const back = dir + Math.PI;
  const len = r * 2.6;
  for (let i = 6; i >= 1; i--) {
    const k = i / 6;
    const side = Math.sin(wave + k * 4.2) * r * 0.5 * k;
    const tx = x + Math.cos(back) * len * k - Math.sin(back) * side;
    const ty = y + Math.sin(back) * len * k + Math.cos(back) * side;
    const tr = r * (1 - k * 0.75);
    let c = G[2];
    if (k > 0.55) {
      c = G[1];
    }
    p.circle(tx, ty, tr, c, a * (1 - k * 0.5));
  }
  // 머리: 짙은 테두리, 밝은 몸, 흰 윗부분
  p.circle(x, y, r + 1, G[0], a);
  p.circle(x, y, r, G[2], a);
  p.circle(x - r * 0.25, y - r * 0.3, r * 0.55, G[3], a);
  // 눈: 날아가는 쪽으로 쏠린 두 개의 검은 점
  const ex = Math.cos(dir) * r * 0.25;
  const ey = Math.sin(dir) * r * 0.2;
  const eyeA = Math.min(1, a * 1.5);
  p.dot(x + ex - r * 0.35, y + ey - r * 0.05, G[0], eyeA);
  p.dot(x + ex + r * 0.35, y + ey - r * 0.05, G[0], eyeA);
  if (r >= 6) {
    p.dot(x + ex - r * 0.35, y + ey + r * 0.25, G[0], eyeA);
    p.dot(x + ex + r * 0.35, y + ey + r * 0.25, G[0], eyeA);
  }
}

export function draw(p, t, o) {
  const C = fit(o.palette, 7);
  const G = fit(o.ghost, 4);
  const S = o.seed;

  // 1) 맞은 순간의 보랏빛 섬광
  const flash = 1 - seg(t, 0, 0.12);
  if (flash > 0) {
    p.circle(0, 0, 4 + 8 * flash, C[5], flash);
    p.glow(0, 0, 30, C[4], 0.5 * flash);
  }

  // 2) 큰 영혼: 바닥에서 솟아 부풀며 떤다
  if (t > 0.05 && t < Burst + 0.02) {
    const k = seg(t, 0.05, Burst);
    const r = 5 + 9 * ease.outCubic(k);
    const shake = Math.sin(t * 90) * 1.5 * k;
    const y = -6 - 16 * ease.outQuad(k);
    // 솟는 길에 남는 혼 김
    for (let i = 0; i < 8; i++) {
      const yy = y + r + i * 3;
      p.dot(Math.sin(t * 20 + i) * 3, yy, C[3 + (i % 2)], 0.8 - i * 0.08);
    }
    ghost(p, G, shake, y, r, -Math.PI / 2, t * 18, 1);
    p.glow(0, y, r * 3, C[4], 0.35);
  }

  // 3) 터짐: 흰 섬광과 보랏빛 충격 고리 두 겹
  const bt = t - Burst;
  if (bt >= 0) {
    const f = 1 - seg(bt, 0, 0.14);
    if (f > 0) {
      p.circle(0, -22, 6 + 14 * (1 - f), WHITE, f);
      p.glow(0, -22, 50, C[5], 0.7 * f);
    }
    const ring = seg(bt, 0, 0.4);
    if (ring < 1) {
      p.ring(0, -16, 8 + 60 * ease.outCubic(ring), 2, C[4], 1 - ring);
      p.ring(0, -16, 6 + 42 * ease.outCubic(ring), 1, C[5], 1 - ring);
    }
    // 혼 조각: 짧게 튀는 보랏빛 점
    for (let i = 0; i < 16; i++) {
      const g = rng(S, i, 3);
      const a = g.range(0, TAU);
      const v = g.range(60, 140);
      const life = g.range(0.2, 0.45);
      if (bt < life) {
        const d = v * bt * (1 - bt / life * 0.4);
        p.dot(Math.cos(a) * d, -16 + Math.sin(a) * d * 0.8, C[4 + (i % 3)], 1 - bt / life);
      }
    }

    // 4) 유령 무리: 휘어진 길로 퍼져 나가며 위아래로 출렁이고, 끝에 흐려진다
    for (let i = 0; i < 7; i++) {
      const g = rng(S, i, 17);
      const base = (i / 7) * TAU + g.range(-0.3, 0.3);
      const curl = g.range(1.2, 2.2) * (i % 2 === 0 ? 1 : -1);
      const speed = g.range(0.85, 1.15);
      const life = g.range(0.95, 1.25);
      const k = seg(bt, 0, life);
      if (k > 0 && k < 1) {
        const travel = ease.outQuad(k) * 66 * speed;
        const ang = base + curl * k;
        const bob = Math.sin(bt * 9 + i) * 3;
        const x = Math.cos(ang) * travel;
        const y = -16 + Math.sin(ang) * travel * 0.75 + bob - k * 10;
        // 날아가는 방향: 길의 접선
        const dir = ang + Math.PI / 2 * Math.sign(curl) * 0.6;
        const r = 3.5 + 2.5 * bump(k, 0, 1) + (1 - k) * 1.5;
        const fade = 1 - seg(k, 0.65, 1);
        ghost(p, G, x, y, r, dir, bt * 14 + i, fade);
        p.glow(x, y, r * 2.5, C[4], 0.18 * fade);
      }
    }
  }
}
