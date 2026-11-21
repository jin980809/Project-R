// 전사 강화 평타(검기)용 프로젝트 전용 이펙트: 초승달 모양의 베기 기운이 오른쪽으로 날아간다.
// pixelfx 라이브러리에 날아가는 검기 그림이 없어 여기서 그린다. export.mjs가 "local/sword-wave"로 읽는다.
// 초승달 앞쪽 끝이 (0, 0)이고, 이펙트 규칙(오른쪽을 본다, Math.random을 쓰지 않는다, 반복이 이어진다)을 따른다.
import { fit, cycle, fly, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Sword wave",
  desc: "A crescent of cutting force flying right: a white-hot leading edge, a colored body thinning to sharp horns, two fading afterimages and short streaks behind.",
  tags: ["melee", "slash", "wave", "projectile", "directional", "loop"],
  size: [96, 72],
  origin: [70, 36],
  duration: 0.4,
  loop: true,
  fps: 30,
  bg: "dark",
  options: { palette: ["#1a2a48", "#2e4a80", "#4a78c0", "#78aaf0", "#b0d4ff", "#e4f2ff", "#ffffff"], height: 20, thick: 7, seed: 3 },
};

// 초승달 한 장: 앞쪽 원(중심 (dx - R, 0))의 안쪽이면서 뒤쪽 원(중심을 thick만큼 더 뒤로)의 바깥인 칸.
// edge는 앞쪽 테두리에서 몇 칸까지 흰색으로 칠할지, dim은 잔상 단계(0이면 본체, 1·2면 점점 어두운 단색).
// 반투명은 점무늬로 찍혀 지저분해 보이므로 잔상은 어두운 단색으로 칠한다.
function crescent(p, C, dx, R, thick, edge, dim) {
  const cx1 = dx - R;
  const cx2 = cx1 - thick;
  p.shade(dx - R - thick - 2, -R - 1, dx + 1, R + 1, (x, y) => {
    const d1 = Math.hypot(x - cx1, y);
    const d2 = Math.hypot(x - cx2, y);
    if (d1 > R + 0.5 || d2 < R - 0.5) {
      return;
    }
    // 앞 테두리에서의 깊이(0이면 맨 앞), 뿔 끝으로 갈수록 가늘어진다.
    const depth = R - d1;
    const tip = Math.abs(y) / R;
    let c = C[3];
    if (depth < edge * (1 - tip * 0.6)) {
      c = WHITE;
    } else if (depth < edge + 1.5) {
      c = C[5];
    } else if (depth < edge + 3) {
      c = C[4];
    }
    if (tip > 0.85) {
      c = C[4];
    }
    if (dim === 1) {
      c = C[2];
    } else if (dim === 2) {
      c = C[1];
    }
    return c;
  });
}

export function draw(p, t, o) {
  const C = fit(o.palette, 7), D = meta.duration, S = o.seed, R = o.height, T = o.thick, ph = t / D;
  const wobble = Math.sin(ph * TAU * 2);

  // 뒤로 흐르는 짧은 줄기(검기가 지나간 자국)
  cycle(t, D, 10, S + 1, (g) => g.range(0.12, 0.22), (age, life, r) => {
    const [x, y] = fly(r.range(-T - 4, -T), r.range(-R * 0.8, R * 0.8), -r.range(160, 240), 0, age, 0, 0);
    const k = age / life;
    p.line(x, y, x - 3 - 4 * (1 - k), y, k < 0.35 ? C[5] : C[3], (1 - k) * 0.8);
  });

  // 흐려지는 잔상 두 장
  crescent(p, C, -T - 7, R - 1, T - 2, 0, 2);
  crescent(p, C, -T * 0.5 - 3, R - 0.5, T - 1, 1, 1);

  // 본체: 두께가 조금씩 숨쉬듯 바뀐다
  crescent(p, C, 0, R, T + Math.round(wobble * 0.6), 2, 0);

  // 빛: 몸통을 따라 은은하게, 앞 테두리는 밝게
  p.glow(-T, 0, R + 4, C[2], 0.2);
  p.glowLine(-2, -R * 0.6, -2, R * 0.6, 5, C[5], 0.25 + 0.08 * wobble);
}
