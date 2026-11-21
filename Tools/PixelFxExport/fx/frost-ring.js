// 서리 폭발(StaffNova)용 프로젝트 전용 이펙트: 위에서 내려다본 원형 서리 폭발.
// 가운데 눈꽃이 번쩍인 뒤 얼음 고리가 판정 반지름까지 퍼지고, 고리가 지나간 자리에 바깥을 향한 얼음 결정이 두 겹으로 솟으며
// 바닥이 서리로 덮였다가 결정이 부서져 사라진다. export.mjs가 "local/frost-ring"으로 읽는다.
// 가운데가 (0, 0)이고, 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다. 판정 원과 맞도록 찌그러뜨리지 않은 원이다.
import { fit, rng, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Frost ring",
  desc: "A top-down frost burst: a snowflake flares at the centre, a ring of ice races out to the edge, two rings of outward-pointing crystals spring up behind it over a frosted floor, then shatter and melt away.",
  tags: ["ice", "nova", "aoe", "frost", "magic", "circle", "one-shot"],
  size: [184, 184],
  origin: [92, 92],
  duration: 1.6,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#151a3d", "#212f6b", "#2c58aa", "#3a8bd8", "#62c8f4", "#a6ecff", "#dcfbff", "#ffffff"], radius: 84, seed: 4 },
};

const clamp01 = (v) => Math.max(0, Math.min(1, v));

// 바깥을 향한 얼음 결정 하나: (x, y)에서 각도 a 쪽으로 길이 len, 폭 wid인 길쭉한 마름모. 위·왼쪽 면은 밝게.
function crystal(p, C, x, y, a, len, wid) {
  const ca = Math.cos(a);
  const sa = Math.sin(a);
  const r = len + wid;
  p.shade(x - r, y - r, x + r, y + r, (px, py) => {
    const dx = px - x;
    const dy = py - y;
    const u = dx * ca + dy * sa;
    const v = -dx * sa + dy * ca;
    if (u < -wid * 0.6 || u > len) {
      return;
    }
    let half = wid * (1 - u / len);
    if (u < 0) {
      half = wid * (1 + u / (wid * 0.6));
    }
    if (Math.abs(v) > half + 0.5) {
      return;
    }
    if (Math.abs(v) > half - 0.6) {
      return C[2];
    }
    if (u > len * 0.7) {
      return WHITE;
    }
    return v < 0 ? C[6] : C[4];
  });
}

export function draw(p, t, o) {
  const C = fit(o.palette, 8), R = o.radius, S = o.seed;
  const grow = clamp01((t - 0.08) / 0.32);
  const front = R * (1 - (1 - grow) * (1 - grow));
  const melt = clamp01((t - 1.05) / 0.5);

  // 서리 바닥: 고리가 지나간 안쪽을 점무늬 서리로 덮는다(가장자리는 성기게). floor 옵션(0~1)으로 촘촘한 정도를 줄인다.
  let floorDensity = 1;
  if (o.floor !== undefined) {
    floorDensity = o.floor;
  }
  if (t > 0.1) {
    p.shade(-R, -R, R, R, (x, y) => {
      const d = Math.hypot(x, y);
      if (d > front) {
        return;
      }
      const k = d / R;
      const g = ((Math.floor(x) * 7 + Math.floor(y) * 13) & 7);
      const density = Math.round((7 - Math.floor(k * 5) - Math.floor(melt * 7)) * floorDensity);
      if (g < density - 3) {
        return C[2];
      }
      if (g < density) {
        return C[1];
      }
    });
  }

  // 퍼지는 얼음 고리(두께 3: 바깥 흰색, 안쪽 하늘색)
  if (grow < 1 || t < 0.6) {
    const fade = clamp01((0.7 - t) / 0.3);
    if (fade > 0) {
      p.shade(-front - 3, -front - 3, front + 3, front + 3, (x, y) => {
        const d = Math.hypot(x, y);
        const e = front - d;
        if (e < -0.5 || e > 3) {
          return;
        }
        if (e < 1) {
          return WHITE;
        }
        return fade > 0.5 ? C[5] : C[3];
      });
      p.glowRing(0, 0, front, 6, C[5], 0.35 * fade);
    }
  }

  // 얼음 결정 두 겹: 고리가 지나가면 솟고, 녹을 때 짧아지며 사라진다
  const rings = [[R * 0.86, 20, 16, 4.5], [R * 0.52, 12, 12, 3.6]];
  for (let j = 0; j < rings.length; j++) {
    const [rr, count, len, wid] = rings[j];
    for (let i = 0; i < count; i++) {
      const g = rng(S, j * 50 + i, 3);
      const a = (i / count) * TAU + g.range(-0.12, 0.12);
      const r0 = rr * g.range(0.92, 1.05);
      if (front < r0) {
        continue;
      }
      // 솟는 정도: 고리가 지나간 뒤 0.1초에 걸쳐 자란다
      const passed = (front - r0) / (R * 0.25);
      const rise = clamp01(passed + (t > 0.45 ? 1 : 0));
      const size = rise * (1 - melt) * g.range(0.8, 1.2);
      if (size < 0.15) {
        continue;
      }
      crystal(p, C, Math.cos(a) * r0, Math.sin(a) * r0, a, len * size, wid * Math.max(0.5, size));
    }
  }

  // 가운데 눈꽃: 처음에 번쩍이고 점점 작아진다
  const flare = clamp01(1 - t / 0.55);
  if (flare > 0) {
    const arm = 6 + 10 * flare;
    for (let k = 0; k < 6; k++) {
      const a = (k / 6) * TAU + t * 2;
      const x1 = Math.cos(a) * arm;
      const y1 = Math.sin(a) * arm;
      p.line(0, 0, x1, y1, WHITE);
      p.line(x1 * 0.6, y1 * 0.6, x1 * 0.6 + Math.cos(a + 0.8) * 3, y1 * 0.6 + Math.sin(a + 0.8) * 3, C[6]);
      p.line(x1 * 0.6, y1 * 0.6, x1 * 0.6 + Math.cos(a - 0.8) * 3, y1 * 0.6 + Math.sin(a - 0.8) * 3, C[6]);
    }
    p.glow(0, 0, 20 + 30 * flare, C[5], 0.5 * flare);
  }

  // 녹을 때 흩어지는 얼음 조각
  if (melt > 0) {
    for (let i = 0; i < 24; i++) {
      const g = rng(S, i, 9);
      const a = g.range(0, TAU);
      const r = g.range(0.4, 1) * R + melt * 8;
      const c = melt < 0.5 ? C[6] : C[4];
      if (g.next() > melt * 0.9) {
        p.px(Math.cos(a) * r, Math.sin(a) * r - melt * 6, c);
      }
    }
  }
}
