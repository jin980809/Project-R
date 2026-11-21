// 도약 찍기(SpearLeap) 착지용 프로젝트 전용 이펙트: 위에서 내려다본 원형 착지 충격.
// 창이 꽂힌 자리가 하얗게 번쩍이고, 패인 자리에서 달아오른 금이 사방으로 갈라지며, 금빛 충격파 고리가 판정 반지름까지 퍼진다.
// 돌조각이 포물선을 그리며 튀었다 떨어지고, 먼지 고리가 바깥으로 밀려난 뒤 금이 식어 사라진다. export.mjs가 "local/leap-crater"로 읽는다.
// 가운데가 (0, 0)이고, 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다. 판정 원과 맞도록 찌그러뜨리지 않은 원이다.
// pixelfx는 반투명을 점무늬로 바꾸므로 잔상·먼지는 어두운 단색으로 그린다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Leap crater",
  desc: "A top-down heavy landing: a white flash where the spear strikes, glowing cracks split out from a small crater, a golden shockwave ring races to the edge, rocks arc out and bounce, a dust ring rolls away and the cracks cool off.",
  tags: ["impact", "landing", "slam", "earth", "aoe", "circle", "one-shot"],
  size: [184, 184],
  origin: [92, 92],
  duration: 1.2,
  loop: false,
  fps: 30,
  bg: "dark",
  options: {
    // 흙(어두운 쪽부터 밝은 쪽) + 달아오른 금빛
    palette: ["#1a120e", "#33241a", "#55402c", "#7c6040", "#a88454", "#ff7a26", "#ffc24a", "#fff2b0"],
    radius: 84,
    seed: 7,
  },
};

const clamp01 = (v) => Math.max(0, Math.min(1, v));

export function draw(p, t, o) {
  const C = fit(o.palette, 8), R = o.radius, S = o.seed;
  const cool = clamp01((t - 0.35) / 0.7);
  const vanish = clamp01((t - 0.9) / 0.3);

  // 먼지 고리: 바깥으로 밀려나며 퍼지는 흙먼지 덩어리(단색 2단)
  const dustT = clamp01(t / 0.9);
  if (dustT > 0 && vanish < 1) {
    const dr = R * (0.35 + 0.75 * (1 - (1 - dustT) * (1 - dustT)));
    for (let i = 0; i < 18; i++) {
      const g = rng(S, i, 21);
      const a = (i / 18) * TAU + g.range(-0.15, 0.15);
      const r = dr * g.range(0.85, 1.05);
      const size = (5 + 4 * g.next()) * (1 - dustT * 0.6) * (1 - vanish);
      if (size < 1) {
        continue;
      }
      const x = Math.cos(a) * r;
      const y = Math.sin(a) * r;
      p.disc(x, y + 1, size, C[2], 1);
      p.disc(x - 1, y - 1, size * 0.7, C[3], 1);
    }
  }

  // 패인 자리: 어두운 구덩이와 밝은 테두리
  const crater = clamp01(t / 0.06) * (1 - vanish);
  if (crater > 0) {
    const cr = 13 * crater;
    p.disc(0, 0, cr + 2, C[4], 1);
    p.disc(0, 0, cr, C[1], 1);
    p.disc(1, 1, cr * 0.6, C[0], 1);
  }

  // 갈라지는 금: 지그재그로 뻗고, 처음엔 달아올랐다가 식으며 어두워진다
  const crackGrow = clamp01((t - 0.02) / 0.16);
  if (crackGrow > 0 && vanish < 1) {
    for (let i = 0; i < 9; i++) {
      const g = rng(S, i, 33);
      const base = (i / 9) * TAU + g.range(-0.2, 0.2);
      const len = R * g.range(0.45, 0.8) * crackGrow;
      let x = Math.cos(base) * 10;
      let y = Math.sin(base) * 10;
      let a = base;
      const steps = 5;
      for (let s = 0; s < steps; s++) {
        a += g.range(-0.45, 0.45);
        const seg = len / steps;
        const nx = x + Math.cos(a) * seg;
        const ny = y + Math.sin(a) * seg;
        let c = C[7];
        if (cool > 0.2) {
          c = C[6];
        }
        if (cool > 0.45) {
          c = C[5];
        }
        if (cool > 0.75) {
          c = C[0];
        }
        p.line(x, y + 1, nx, ny + 1, C[0]);
        p.line(x, y, nx, ny, c);
        p.line(x + 1, y, nx + 1, ny, c);
        // 곁가지 한 줄
        if (s === 2) {
          const ba = a + g.range(0.6, 1) * (g.next() < 0.5 ? -1 : 1);
          p.line(nx, ny, nx + Math.cos(ba) * seg * 0.7, ny + Math.sin(ba) * seg * 0.7, c);
        }
        x = nx;
        y = ny;
      }
    }
  }

  // 충격파 고리: 금빛 두 겹(바깥 밝게)이 판정 반지름까지 빠르게 퍼진다
  const waveT = clamp01((t - 0.03) / 0.3);
  if (waveT > 0 && waveT < 1) {
    const wr = R * (1 - (1 - waveT) * (1 - waveT));
    let w = 5;
    if (waveT > 0.6) {
      w = 3;
    }
    p.shade(-wr - 2, -wr - 2, wr + 2, wr + 2, (x, y) => {
      const e = wr - Math.hypot(x, y);
      if (e < -0.5 || e > w) {
        return;
      }
      if (e < 1.2) {
        return waveT < 0.5 ? WHITE : C[7];
      }
      return waveT < 0.5 ? C[6] : C[5];
    });
    p.glowRing(0, 0, wr, 6, C[6], 0.4 * (1 - waveT));
    // 두 번째 고리(조금 늦게, 가늘게)
    const w2 = clamp01((t - 0.12) / 0.3);
    if (w2 > 0 && w2 < 1) {
      p.ring(0, 0, R * 0.8 * (1 - (1 - w2) * (1 - w2)), 1, C[5]);
    }
  }

  // 튀는 돌조각: 바깥으로 날아가며 포물선(화면 위쪽이 높이), 땅에 닿으면 한 번 튄다
  if (vanish < 1) {
    for (let i = 0; i < 16; i++) {
      const g = rng(S, i, 47);
      const a = g.range(0, TAU);
      const dist = R * g.range(0.35, 0.95);
      const life = g.range(0.45, 0.65);
      const k = clamp01(t / life);
      let h = Math.sin(k * Math.PI) * g.range(14, 26);
      let r = dist * k;
      if (k >= 1) {
        const b = clamp01((t - life) / 0.18);
        r = dist + dist * 0.12 * b;
        h = Math.sin(b * Math.PI) * 4;
      }
      const x = Math.cos(a) * r;
      const y = Math.sin(a) * r;
      const s = 1.2 + g.next() * 1.6;
      // 그림자, 돌, 밝은 윗면
      p.disc(x, y + 1, s, C[0], 1);
      p.disc(x, y - h, s, C[4], 1);
      p.px(x - 1, y - h - 1, C[7]);
    }
  }

  // 꽂히는 순간의 번쩍임과 불티
  const flash = clamp01(1 - t / 0.16);
  if (flash > 0) {
    p.glow(0, 0, 18 + 34 * flash, C[6], 0.6 * flash);
    p.disc(0, 0, 4 + 7 * flash, WHITE, 1);
    for (let k = 0; k < 8; k++) {
      const a = (k / 8) * TAU + 0.3;
      const r0 = 6 + (1 - flash) * 10;
      const r1 = r0 + 6 + 10 * flash;
      p.line(Math.cos(a) * r0, Math.sin(a) * r0, Math.cos(a) * r1, Math.sin(a) * r1, C[7]);
    }
  }
  if (t < 0.6) {
    for (let i = 0; i < 20; i++) {
      const g = rng(S, i, 59);
      const a = g.range(0, TAU);
      const r = (8 + t * g.range(60, 120));
      const life = g.range(0.25, 0.55);
      if (t < life) {
        let c = C[7];
        if (t > life * 0.5) {
          c = C[5];
        }
        p.px(Math.cos(a) * r, Math.sin(a) * r - t * 20, c);
      }
    }
  }
}
