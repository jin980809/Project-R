// 성광(PriestRadiance)용 프로젝트 전용 이펙트: 위에서 본 몸 둘레의 성스러운 빛 폭발.
// 0~0.14초 가운데 흰 금빛 섬광과 긴 십자 반짝임, 그 뒤 금빛 빛 고리가 반경 끝까지 퍼진다.
// 고리가 지나간 바닥에는 천천히 도는 햇살 부채꼴(빛줄기 12갈래)이 깔리고, 고리가 지나간 자리마다
// 작은 빛기둥이 솟았다 사그라든다. 끝에는 금빛 티끌이 떠오르며 흐려진다.
// 서리 폭발(frost-ring)의 얼음 결정과 달리 빛줄기·빛기둥으로 이루어진다.
// 가운데(0, 0)가 캐릭터 발밑이고, export.mjs가 "local/holy-radiance"로 읽는다. 이펙트 규칙(Math.random을 쓰지 않는다)을 따른다.
import { fit, rng, seg, bump, ease, TAU, WHITE } from "../../../../pixelfx/src/core/index.js";

export const meta = {
  name: "Holy radiance",
  desc: "Top-down holy burst: a white-gold flash with a long cross glint, a ring of gold light rolls out, slowly turning sunbeams fan over the floor behind it, little pillars of light spring up where it passes, and gold motes float away.",
  tags: ["holy", "light", "nova", "radial", "top-down", "one-shot"],
  size: [240, 240],
  origin: [120, 120],
  duration: 1.4,
  loop: false,
  fps: 30,
  bg: "dark",
  options: { palette: ["#2a1c08", "#5a3c10", "#9a6c18", "#d8a428", "#ffd84a", "#fff09a", "#fffbe6", "#ffffff"], radius: 108, seed: 8 },
};

export function draw(p, t, o) {
  const C = fit(o.palette, 8);
  const R = o.radius;
  const S = o.seed;
  const ringK = ease.outCubic(seg(t, 0.04, 0.5));
  const ringR = R * ringK;
  const fadeAll = 1 - seg(t, 0.9, 1.4);

  // 1) 바닥 햇살: 고리 안쪽에 12갈래 부채꼴이 천천히 돈다(갈래 사이는 비운다)
  if (ringR > 4 && fadeAll > 0) {
    const turn = t * 0.5;
    p.shade(-ringR, -ringR, ringR, ringR, (x, y) => {
      const d = Math.hypot(x, y);
      if (d > ringR || d < 6) {
        return;
      }
      let a = Math.atan2(y, x) + turn;
      a = ((a % (TAU / 12)) + TAU / 12) % (TAU / 12);
      const w = TAU / 12;
      const center = Math.abs(a - w / 2) / (w / 2);
      // 갈래 폭은 바깥으로 갈수록 좁아진다
      const width = 0.42 - 0.2 * (d / R);
      if (center > width) {
        return;
      }
      // 가운데 가까운 곳은 밝게, 고리 가까운 곳은 짙게. 점무늬로 성기게 칠해 안의 캐릭터가 가려지지 않게 한다.
      if (((Math.floor(x) + Math.floor(y)) & 1) === 0) {
        return;
      }
      const k = d / R;
      if (fadeAll < 0.5 && ((Math.floor(x) * 3 + Math.floor(y)) % 4) === 0) {
        return;
      }
      if (k < 0.3) {
        return C[5];
      }
      if (k < 0.65) {
        return C[4];
      }
      return C[3];
    });
  }

  // 2) 퍼지는 빛 고리: 두께가 얇아지며 끝에서 사그라든다
  if (t > 0.04 && t < 0.75) {
    const k = seg(t, 0.5, 0.75);
    const w = 3 - k * 2;
    p.ring(0, 0, ringR + 2, w + 2, C[3], 1 - k);
    p.ring(0, 0, ringR, w, C[6], 1 - k);
    p.glowRing(0, 0, ringR, 8, C[4], 0.5 * (1 - k));
  }

  // 3) 빛기둥: 고리가 지나간 자리(바깥 둘레 16곳, 안쪽 8곳)에서 솟았다 사그라든다. 위에서 비스듬히 본 세로 줄기.
  for (let i = 0; i < 24; i++) {
    const g = rng(S, i, 5);
    let a = (i / 16) * TAU + g.range(-0.1, 0.1);
    let d = R * g.range(0.82, 0.96);
    if (i >= 16) {
      a = ((i - 16) / 8) * TAU + TAU / 16;
      d = R * g.range(0.45, 0.6);
    }
    const born = 0.04 + 0.46 * ease.outCubic(d / R) * 0.9;
    const life = 0.5;
    const k = seg(t, born, born + life);
    if (k > 0 && k < 1) {
      const x = Math.cos(a) * d;
      const y = Math.sin(a) * d;
      const h = 22 * bump(k, 0, 1) + 4;
      const fade = 1 - seg(k, 0.6, 1);
      p.ellipse(x, y, 4, 2, C[4], fade);
      p.line(x, y, x, y - h, C[6], fade);
      p.line(x - 1, y, x - 1, y - h * 0.7, C[4], fade);
      p.line(x + 1, y, x + 1, y - h * 0.7, C[4], fade);
      p.px(x, y - h - 1, WHITE);
      p.glow(x, y - h * 0.5, 8, C[5], 0.35 * fade);
    }
  }

  // 4) 가운데 섬광과 긴 십자 반짝임
  const flash = 1 - seg(t, 0, 0.3);
  if (flash > 0) {
    const len = 20 + 40 * (1 - flash);
    p.circle(0, 0, 3 + 7 * flash, WHITE, flash);
    p.line(-len, 0, len, 0, C[7], flash);
    p.line(0, -len * 0.8, 0, len * 0.8, C[7], flash);
    p.line(-len * 0.4, -len * 0.4, len * 0.4, len * 0.4, C[5], flash);
    p.line(-len * 0.4, len * 0.4, len * 0.4, -len * 0.4, C[5], flash);
    p.glow(0, 0, 50, C[5], 0.8 * flash);
  }

  // 5) 떠오르는 금빛 티끌
  for (let i = 0; i < 30; i++) {
    const g = rng(S, i, 11);
    const a = g.range(0, TAU);
    const d = R * Math.sqrt(g.next()) * 0.95;
    const born = 0.1 + (d / R) * 0.4;
    const k = seg(t, born, born + 0.8);
    if (k > 0 && k < 1) {
      const x = Math.cos(a) * d + Math.sin(k * 6 + i) * 2;
      const y = Math.sin(a) * d - k * 18;
      let c = C[5];
      if (i % 3 === 0) {
        c = WHITE;
      }
      p.dot(x, y, c, 1 - k);
    }
  }
}
