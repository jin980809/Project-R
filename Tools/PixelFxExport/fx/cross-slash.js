// 십자 베기(SwordCross)용 프로젝트 전용 이펙트: 위에서 본 X자 두 번 베기(오른쪽이 조준 방향).
// 위에서 아래로 한 번, 아래에서 위로 한 번 큰 초승달 칼날이 지나가며 X를 그리고, 두 줄이 엇갈리는 순간
// 교차점이 하얗게 터지며 불티가 흩어지고 충격 고리가 퍼진다. 마지막에 X 자국이 잠시 남았다가 사라진다.
// 가운데(0, 0)가 캐릭터 자리이고, export.mjs가 "local/cross-slash"로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
// pixelfx는 반투명을 점무늬로 바꾸므로 잔상은 어두운 단색으로 그린다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Cross slash",
  desc: "A top-down X cut to the right: a crescent blade sweeps down, a second sweeps up across it, the crossing bursts white with sparks and a shock ring, and a glinting X lingers then fades.",
  tags: ["melee", "slash", "sword", "cross", "directional", "one-shot"],
  size: [176, 160],
  origin: [40, 80],
  duration: 0.7,
  loop: false,
  fps: 30,
  bg: "dark",
  options: {
    palette: ["#141a34", "#24346a", "#3e5ca8", "#6a92e0", "#a6c8ff", "#dcecff", "#ffffff"],
    sparks: ["#ffffff", "#fff3b0", "#ffd25a", "#ff9a3a"],
    reach: 120,
    seed: 6,
  },
};

const clamp01 = (v) => Math.max(0, Math.min(1, v));

// 2차 베지어 위의 점
function bez(a, b, c, u) {
  const v = 1 - u;
  return [v * v * a[0] + 2 * v * u * b[0] + u * u * c[0], v * v * a[1] + 2 * v * u * b[1] + u * u * c[1]];
}

// 칼날 한 줄: head(0~1)까지 그리고, 꼬리 쪽은 어두운 색으로 가늘어진다. fade가 1이면 사라진 상태.
function blade(p, C, a, b, c, head, width, fade) {
  const tail = 1;
  const steps = 70;
  for (let i = steps; i >= 0; i--) {
    const u = head - (i / steps) * tail;
    if (u < 0 || u > 1) {
      continue;
    }
    const k = 1 - i / steps;
    // 끝은 뾰족하게: 칼날 전체 길이 위치(u)와 머리에서 떨어진 정도(k)로 굵기를 정한다.
    const taper = Math.sin(Math.PI * clamp01(u)) * (0.25 + 0.75 * k);
    const w = width * taper * (1 - fade);
    if (w < 0.5) {
      continue;
    }
    const pt = bez(a, b, c, u);
    let col = C[2];
    if (k > 0.35) {
      col = C[3];
    }
    if (k > 0.6) {
      col = C[4];
    }
    if (k > 0.82) {
      col = C[5];
    }
    p.disc(pt[0], pt[1], w, col, 1);
    if (k > 0.7) {
      p.disc(pt[0], pt[1], Math.max(0.5, w * 0.45), WHITE, 1);
    }
  }
}

export function draw(p, t, o) {
  const C = fit(o.palette, 7), S = o.seed, R = o.reach;
  const sparks = o.sparks;
  // 두 칼날의 길: 위에서 아래로(앞쪽으로 휘어짐), 아래에서 위로
  const A0 = [R * 0.12, -R * 0.62], A1 = [R * 0.66, -R * 0.12], A2 = [R * 0.88, R * 0.62];
  const B0 = [R * 0.12, R * 0.62], B1 = [R * 0.66, R * 0.12], B2 = [R * 0.88, -R * 0.62];
  const cross = [R * 0.53, 0];

  const headA = clamp01(t / 0.14);
  const headB = clamp01((t - 0.12) / 0.14);
  const fadeA = clamp01((t - 0.2) / 0.22);
  const fadeB = clamp01((t - 0.32) / 0.22);

  // 남는 X 자국: 가는 선이 잠시 반짝이다 사라진다
  const scar = clamp01((t - 0.28) / 0.1) * (1 - clamp01((t - 0.45) / 0.25));
  if (scar > 0) {
    for (let j = 0; j < 2; j++) {
      const a = j === 0 ? A0 : B0;
      const b = j === 0 ? A1 : B1;
      const c = j === 0 ? A2 : B2;
      for (let i = 0; i <= 40; i++) {
        const u = 0.15 + (i / 40) * 0.7;
        const pt = bez(a, b, c, u);
        if (((i + Math.floor(t * 30)) % 3) !== 0) {
          p.px(pt[0], pt[1], scar > 0.5 ? C[4] : C[2]);
        }
      }
    }
  }

  if (t < 0.45) {
    blade(p, C, A0, A1, A2, headA, 9, fadeA);
  }
  if (t > 0.12 && t < 0.55) {
    blade(p, C, B0, B1, B2, headB, 9, fadeB);
  }

  // 교차점: 두 번째 칼날이 지나가는 순간 터진다
  const burst = clamp01((t - 0.24) / 0.05) * (1 - clamp01((t - 0.3) / 0.2));
  if (burst > 0) {
    p.glow(cross[0], cross[1], 14 + 22 * burst, C[5], 0.55 * burst);
    p.disc(cross[0], cross[1], 3 + 6 * burst, WHITE, 1);
    for (let k = 0; k < 4; k++) {
      const a = k * (TAU / 4) + TAU / 8;
      const len = 10 + 18 * burst;
      p.line(cross[0], cross[1], cross[0] + Math.cos(a) * len, cross[1] + Math.sin(a) * len, WHITE);
    }
    p.line(cross[0] - 8 - 10 * burst, cross[1], cross[0] + 8 + 10 * burst, cross[1], C[5]);
    p.line(cross[0], cross[1] - 6 - 8 * burst, cross[0], cross[1] + 6 + 8 * burst, C[5]);
  }
  // 충격 고리
  const ringT = clamp01((t - 0.24) / 0.25);
  if (ringT > 0 && ringT < 1) {
    p.ring(cross[0], cross[1], 6 + 26 * ringT, ringT < 0.5 ? 2 : 1, ringT < 0.5 ? C[5] : C[3]);
  }
  // 불티: 교차점에서 바깥(주로 앞쪽)으로 튄다
  if (t > 0.24 && t < 0.6) {
    const st = t - 0.24;
    for (let i = 0; i < 18; i++) {
      const g = rng(S, i, 13);
      const a = g.range(-1.6, 1.6) + (g.next() < 0.3 ? Math.PI : 0);
      const v = g.range(70, 150);
      const life = g.range(0.15, 0.32);
      if (st < life) {
        const r = v * st;
        const x = cross[0] + Math.cos(a) * r;
        const y = cross[1] + Math.sin(a) * r;
        const c = sparks[Math.min(sparks.length - 1, Math.floor((st / life) * sparks.length))];
        p.line(x, y, x - Math.cos(a) * 3, y - Math.sin(a) * 3, c);
      }
    }
  }
}
