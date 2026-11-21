// 독 구름(WandMiasma)용 프로젝트 전용 이펙트: 몽글몽글한 독 구름 덩어리.
// 0~1.2초: 가운데 작은 뭉치에서 시작해 둥근 뭉치가 하나씩 부풀어 오르며 뭉게뭉게 퍼진다(나타나는 그림).
// 1.2~3.2초: 뭉치들이 숨쉬듯 부풀었다 줄고 독 거품이 올라와 터진다(반복 그림, 2초 주기로 이어진다).
// 3.2~4.0초: 바깥 뭉치부터 오그라들며 위로 흩어져 사라진다(사라지는 그림).
// 위쪽은 밝게 아래쪽은 보랏빛 그림자로 칠해 입체감을 준다. export.mjs가 "local/toxic-cloud"로 읽는다.
// 구름 가운데 바닥이 (0, 0)이고, 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, cycle, seg, ease, TAU } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Toxic cloud",
  desc: "A puffy cloud of poison billows out from a single puff, breathes in and out with bubbles rising and popping, then shrinks from the edges and drifts apart.",
  tags: ["nature", "poison", "gas", "cloud", "toxic", "area"],
  size: [168, 128],
  origin: [84, 76],
  duration: 4,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#140a1e", "#2c1640", "#4a2f5a", "#4a6a30", "#6c9a34", "#94c440", "#c4e66a", "#ecffb0"], seed: 5 },
};

const IntroEnd = 1.2;
const LoopEnd = 3.2;
const Period = 2;

// 구름 뭉치: 자리(x, y), 반지름, 숨쉬기 위상. 가운데에서 먼 뭉치일수록 늦게 부풀고 일찍 사라진다.
const PUFFS = [
  [-30, -20, 15, 0.1], [0, -26, 18, 0.5], [30, -18, 15, 0.8],
  [-46, -4, 14, 0.3], [-16, -8, 19, 0.65], [16, -6, 19, 0.2], [46, -2, 14, 0.9],
  [-30, 8, 16, 0.45], [2, 10, 18, 0.05], [32, 9, 15, 0.7],
];

// 뭉치가 지금 얼마나 부풀었는지(0~1)와 사라지며 떠오른 거리
function growthOf(i, t) {
  const [x, y] = PUFFS[i];
  const far = Math.min(1, Math.hypot(x, y * 1.4) / 52);
  let grow = 1;
  let lift = 0;
  if (t < IntroEnd) {
    // 가운데부터 차례로: 늦게 시작하는 뭉치는 살짝 넘치게 부풀었다 자리 잡는다
    const start = far * 0.55;
    grow = ease.outBack(seg(t, start, start + 0.6), 1.4);
  } else if (t > LoopEnd) {
    const start = LoopEnd + (1 - far) * 0.3;
    const k = seg(t, start, start + 0.5);
    grow = 1 - ease.inQuad(k);
    lift = k * 14;
  }
  return [grow, lift];
}

function puffAt(i, t) {
  const [x, y, r, w] = PUFFS[i];
  // 반복 구간의 위상(나타나는 동안과 사라지는 동안에도 같은 식으로 움직여 끊기지 않는다)
  const ph = (t - IntroEnd) / Period;
  const s = Math.sin((ph + w) * TAU);
  const [grow, lift] = growthOf(i, t);
  // 부푸는 동안은 가운데 쪽에서 제자리로 밀려 나간다
  const spread = 0.35 + 0.65 * Math.min(1, grow);
  return [x * spread + Math.sin((ph * 0.5 + w) * TAU) * 1.5, y * spread + s * 1.2 - lift, Math.max(0, (r + s * 1.6) * grow)];
}

export function draw(p, t, o) {
  const C = fit(o.palette, 8);
  const n = PUFFS.length;
  const cur = [];
  let total = 0;
  for (let i = 0; i < n; i++) {
    cur.push(puffAt(i, t));
    total += cur[i][2];
  }
  if (total < 1) {
    return;
  }

  // 1) 바닥 그림자: 구름 크기에 맞춘 납작한 보랏빛 타원
  const shadowK = Math.min(1, total / 160);
  if (shadowK > 0.05) {
    p.ellipse(0, 22, 62 * shadowK, 7 * shadowK, C[1]);
  }

  // 2) 바깥 테두리: 모든 뭉치를 2칸 크게 짙은 색으로 먼저 칠한다(하나로 이어진 구름 윤곽)
  for (let i = 0; i < n; i++) {
    const [x, y, r] = cur[i];
    if (r > 0.8) {
      p.circle(x, y, r + 2, C[1]);
    }
  }

  // 3) 몸통: 칸마다 가장 앞으로 솟은 뭉치(높이 + 아래쪽일수록 앞)의 면으로 빛을 계산해 하나로 이어진 구름으로 칠한다
  p.shade(-70, -66, 70, 36, (x, y) => {
    let best = -1;
    let bestZ = -1e9;
    for (let i = 0; i < n; i++) {
      const [cx, cy, r] = cur[i];
      if (r > 0.8) {
        const dd = ((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (r * r);
        if (dd <= 1) {
          const z = Math.sqrt(1 - dd) * r + cy * 0.6;
          if (z > bestZ) {
            bestZ = z;
            best = i;
          }
        }
      }
    }
    if (best < 0) {
      return;
    }
    const [cx, cy, r] = cur[best];
    const dx = (x - cx) / r;
    const dy = (y - cy) / r;
    const nz = Math.sqrt(Math.max(0, 1 - dx * dx - dy * dy));
    // 구름 전체의 위쪽일수록 밝다(뭉치마다 따로 빛나지 않게)
    const light = -dx * 0.3 - dy * 0.45 + nz * 0.45 - (y + 10) / 90;
    if (dy > 0.55 && light < 0.35) {
      return C[2];
    }
    if (light < 0.2) {
      return C[3];
    }
    if (light < 0.55) {
      return C[4];
    }
    if (light < 0.82) {
      return C[5];
    }
    return C[6];
  });

  // 4) 퍼지는 동안 뭉치가 터져 나오는 자리에서 작은 김이 튄다
  if (t < IntroEnd) {
    for (let i = 0; i < n; i++) {
      const [grow] = growthOf(i, t);
      if (grow > 0.05 && grow < 0.7) {
        const [x, y, r] = cur[i];
        const k = grow / 0.7;
        for (let j = 0; j < 4; j++) {
          const a = (j / 4) * TAU + i;
          const d = r + 2 + k * 6;
          p.px(x + Math.cos(a) * d, y + Math.sin(a) * d * 0.8, C[5]);
        }
      }
    }
  }

  // 5) 올라와 터지는 독 거품(반복 구간 주기에 맞춰 이어진다). 나타나는 동안과 사라지는 동안에는 줄어든다.
  let bubbleK = 1;
  if (t < IntroEnd) {
    bubbleK = seg(t, 0.6, IntroEnd);
  } else if (t > LoopEnd) {
    bubbleK = 1 - seg(t, LoopEnd, LoopEnd + 0.4);
  }
  if (bubbleK > 0) {
    cycle(t - IntroEnd, Period, 9, o.seed, (g) => g.range(0.5, 0.9), (age, life, rr, i) => {
      if (i / 9 < bubbleK) {
        const k = age / life;
        const x = rr.range(-44, 44) + Math.sin(k * TAU) * 2;
        const y = rr.range(-10, 6) - k * 30;
        const r = 1.5 + k * 1.5;
        if (k < 0.85) {
          p.ring(x, y, r, 1, C[6]);
          p.px(x - r * 0.4, y - r * 0.4, C[7]);
        } else {
          const s = r + 2;
          p.px(x - s, y, C[6]);
          p.px(x + s, y, C[6]);
          p.px(x, y - s, C[6]);
          p.px(x, y + s, C[6]);
        }
      }
    });
  }

  // 6) 사라질 때 위로 흩어지는 독 김
  if (t > LoopEnd) {
    const k = seg(t, LoopEnd, 4);
    for (let i = 0; i < 18; i++) {
      const a = i * 2.39996;
      const r0 = 10 + (i % 6) * 8;
      const x = Math.cos(a) * r0 * (1 + k * 0.4);
      const y = Math.sin(a) * r0 * 0.5 - 8 - k * 26 - (i % 3) * 4;
      p.dot(x, y, C[4 + (i % 3)], 1 - k);
    }
  }

  p.glow(0, -6, 60 * Math.min(1, total / 170), C[5], 0.12);
}
