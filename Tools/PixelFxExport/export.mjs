// pixelfx(https://github.com/patch-gg/pixelfx) 이펙트를 스킬 이펙트 프레임으로 내보낸다.
// 결과: Assets/4.Sprite/Fx/SkillVfx/<스킬>/<부위>_NN.png + meta.txt의 fps·크기 값 (TowerBootstrap.LoadSkillVfx가 읽는다)
//
//   node export.mjs                         skills.mjs의 모든 스킬을 내보내고 Tools/PixelFxExport/preview/에 확인용 그림을 만든다
//   node export.mjs --only SwordSpin,AxeWhirl
//   node export.mjs --preview-only          파일은 쓰지 않고 확인용 그림만 만든다
//   node export.mjs --survey melee/spin-slash,ice/frost-nova   원본 이펙트를 시간축으로 늘어놓아 본다(자를 구간 고르기용)
//   --pixelfx <경로>                       pixelfx 체크아웃 위치(기본: 프로젝트 옆 ../pixelfx)
//
// 그림 규칙(SkillFxPlayer와 맞춘다)
// - 프레임 가운데가 기준점(anchor)이고, 프레임 가로 절반이 기준점에서 그림이 가장 멀리 뻗은 거리다.
//   게임은 프레임 가로를 지름(반경 x 2)으로 늘리므로 cast_scale 1이면 그림 끝이 스킬 반경에 닿는다.
// - 방향 있는 그림은 오른쪽을 본다. 기준점은 캐릭터 허리(anchor: "waist") 또는 그림 왼쪽 끝(anchor: "left")이다.
// - 한 번 재생 그림을 중간에서 자르면 끝 부분(fade 비율)을 흐리게 해 툭 끊기지 않게 한다.

import { readFileSync, writeFileSync, existsSync, mkdirSync, readdirSync, unlinkSync } from "node:fs";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(here, "..", "..");
const vfxRoot = join(projectRoot, "Assets", "4.Sprite", "Fx", "SkillVfx");
const previewRoot = join(here, "preview");

const args = process.argv.slice(2);
const flags = {};
for (let i = 0; i < args.length; i++) {
  const name = args[i].slice(2);
  if (name === "preview-only") {
    flags[name] = true;
  } else {
    flags[name] = args[i + 1];
    i++;
  }
}

let pixelfxDir = resolve(projectRoot, "..", "pixelfx");
if (flags.pixelfx) {
  pixelfxDir = resolve(flags.pixelfx);
}
const fxlib = await import(pathToFileURL(join(pixelfxDir, "tools", "lib", "fxlib.mjs")).href);
const { loadEffect, renderFrames, canvas, save } = fxlib;
const { PAL } = await import(pathToFileURL(join(pixelfxDir, "src", "core", "color.js")).href);

const RenderFps = 60;
const AlphaCut = 24;
const WaistHeight = 19;
const PreviewBg = [34, 30, 40, 255];
// 마을·던전 픽셀아트와 같은 밀도: 32픽셀 = 1유닛
const PixelsPerUnit = 32;

// 원본 이펙트를 렌더하고 구간에서 프레임을 고른다. 결과 프레임은 원본 캔버스 크기(RGBA)다.
// opts.palette에 이름("fire")을 주면 pixelfx의 PAL 색 묶음을 쓴다.
async function renderPart(part) {
  // "local/이름"은 pixelfx에 없는 프로젝트 전용 이펙트(Tools/PixelFxExport/fx/이름.js)다.
  let fx = null;
  if (part.id.startsWith("local/")) {
    fx = await loadEffect(part.id.slice(6), join(here, "fx"));
  } else {
    fx = await loadEffect(part.id);
  }
  const options = { ...(part.opts ?? {}) };
  for (const key of Object.keys(options)) {
    if (options[key] === undefined) {
      delete options[key];
    }
  }
  if (typeof options.palette === "string") {
    options.palette = PAL[options.palette];
  }
  const renderOptions = { fps: RenderFps, bg: "none", options: options };
  if (part.seed !== undefined) {
    renderOptions.seed = part.seed;
  }
  const result = renderFrames(fx, renderOptions);
  let from = 0;
  if (part.from !== undefined) {
    from = part.from;
  }
  let to = fx.meta.duration;
  if (part.to !== undefined) {
    to = part.to;
  }
  const count = part.frames;
  let speed = 1;
  if (part.speed !== undefined) {
    speed = part.speed;
  }
  const fps = count / ((to - from) / speed);
  if (part.scatter !== undefined) {
    return scatterFrames(part, fx, result, from, to, fps);
  }
  const picked = [];
  for (let i = 0; i < count; i++) {
    const time = from + ((to - from) * i) / count;
    const index = Math.min(result.n - 1, Math.max(0, Math.round(time * RenderFps)));
    picked.push(new Uint8ClampedArray(result.frames[index].clear));
  }
  // smear: 앞 프레임을 이 비율로 흐리게 남겨 지나간 자리에 자국이 이어지게 한다(돌진 길).
  if (part.smear !== undefined) {
    for (let i = 1; i < picked.length; i++) {
      const trailFrame = new Uint8ClampedArray(picked[i - 1]);
      multiplyAlpha(trailFrame, part.smear);
      blendOver(trailFrame, result.w, picked[i], result.w, result.h, 0, 0);
      picked[i] = trailFrame;
    }
  }
  return { fx, w: result.w, h: result.h, frames: picked, fps: fps, origin: fx.meta.origin };
}

// 같은 이펙트를 원 안 여러 자리에 시간차를 두고 겹쳐 넓은 장판을 만든다(그림 픽셀 크기는 그대로).
// scatter: { count, radius(픽셀), squash(세로 비율, 기본 1), center(가운데에도 하나 둘지) }
// 각 사본의 시간은 구간 길이로 되감기므로, 구간이 이펙트 전체(빈 그림 → 빈 그림)면 반복이 끊기지 않는다.
function scatterFrames(part, fx, result, from, to, fps) {
  const sc = part.scatter;
  let squash = 1;
  if (sc.squash !== undefined) {
    squash = sc.squash;
  }
  const spots = [];
  let firstRing = 0;
  if (sc.center) {
    spots.push([0, 0]);
    firstRing = 1;
  }
  for (let i = firstRing; i < sc.count; i++) {
    // 해바라기 배치: 원 안에 고르게 퍼진다
    const k = (i - firstRing + 0.5) / (sc.count - firstRing);
    const r = Math.sqrt(k) * sc.radius;
    const a = i * 2.39996323;
    spots.push([Math.round(Math.cos(a) * r), Math.round(Math.sin(a) * r * squash)]);
  }
  const padX = sc.radius;
  const padY = Math.ceil(sc.radius * squash);
  const w = result.w + padX * 2;
  const h = result.h + padY * 2;
  const length = to - from;
  const frames = [];
  // 위쪽(먼) 사본부터 그려 아래쪽(가까운) 사본이 위에 덮이게 한다
  const order = spots.map((p, i) => i).sort((a, b) => spots[a][1] - spots[b][1]);
  for (let f = 0; f < part.frames; f++) {
    const out = new Uint8ClampedArray(w * h * 4);
    for (const i of order) {
      const delay = (i * 0.61803398875 * length) % length;
      let local = (f / part.frames) * length - delay;
      local = ((local % length) + length) % length;
      const index = Math.min(result.n - 1, Math.max(0, Math.round((from + local) * RenderFps)));
      blendOver(out, w, result.frames[index].clear, result.w, result.h, padX + spots[i][0], padY + spots[i][1]);
    }
    frames.push(out);
  }
  return { fx, w: w, h: h, frames: frames, fps: fps, origin: [fx.meta.origin[0] + padX, fx.meta.origin[1] + padY] };
}

function blendOver(dst, dw, src, sw, sh, ox, oy) {
  for (let y = 0; y < sh; y++) {
    for (let x = 0; x < sw; x++) {
      const s = (y * sw + x) * 4;
      const sa = src[s + 3] / 255;
      if (sa > 0) {
        const d = ((y + oy) * dw + x + ox) * 4;
        const da = dst[d + 3] / 255;
        const oa = sa + da * (1 - sa);
        for (let c = 0; c < 3; c++) {
          dst[d + c] = Math.round((src[s + c] * sa + dst[d + c] * da * (1 - sa)) / oa);
        }
        dst[d + 3] = Math.round(oa * 255);
      }
    }
  }
}

function multiplyAlpha(rgba, k) {
  for (let i = 3; i < rgba.length; i += 4) {
    rgba[i] = Math.round(rgba[i] * k);
  }
}

// 4x4 순서 디더(픽셀이 점점이 나타나고 사라지게)
const Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
function bayerAt(x, y) {
  return (Bayer[(y & 3) * 4 + (x & 3)] + 0.5) / 16;
}

// 그림을 기준점(anchor) 둘레로 scale배 늘이거나 줄이고(가까운 픽셀), 디더 문턱(cut: 0 모두 남김 ~ 1 모두 지움)과 투명도를 준다.
function transformFrame(src, w, h, anchor, scale, cut, alpha) {
  const out = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (bayerAt(x, y) >= cut) {
        const sx = Math.round(anchor[0] + (x - anchor[0]) / scale);
        const sy = Math.round(anchor[1] + (y - anchor[1]) / scale);
        if (sx >= 0 && sy >= 0 && sx < w && sy < h) {
          const s = (sy * w + sx) * 4;
          const d = (y * w + x) * 4;
          out[d] = src[s];
          out[d + 1] = src[s + 1];
          out[d + 2] = src[s + 2];
          out[d + 3] = Math.round(src[s + 3] * alpha);
        }
      }
    }
  }
  return out;
}

// 장판의 나타나는/사라지는 그림. 구간(from, to)이면 같은 이펙트를 그 구간으로 다시 렌더하고, mode면 반복 그림을 바꿔 만든다.
async function sideFrames(part, spec, rendered, anchor, isIntro) {
  if (spec === undefined) {
    return { frames: [], fps: 0 };
  }
  if (spec.from !== undefined) {
    let speed = part.speed;
    if (spec.speed !== undefined) {
      speed = spec.speed;
    }
    const seg = await renderPart({ ...part, from: spec.from, to: spec.to, frames: spec.frames, speed: speed, smear: undefined });
    if (seg.w !== rendered.w || seg.h !== rendered.h) {
      throw new Error(part.id + ": intro/outro 칸 크기가 반복 그림과 다릅니다");
    }
    return { frames: seg.frames, fps: seg.fps };
  }
  const loop = rendered.frames;
  const n = loop.length;
  const count = Math.max(3, Math.round(spec.time * rendered.fps));
  const frames = [];
  for (let i = 0; i < count; i++) {
    let src = null;
    let k = 0;
    if (isIntro) {
      // 마지막 나타나는 그림 다음이 반복 그림 첫 장이 되게 앞 장들을 쓴다.
      src = loop[(((n - count + i) % n) + n) % n];
      k = (i + 1) / count;
    } else {
      src = loop[i % n];
      k = i / count;
    }
    let scale = 1;
    let cut = 0;
    let alpha = 1;
    if (spec.mode === "grow") {
      const e = 1 - Math.pow(1 - k, 3);
      scale = 0.2 + 0.8 * e;
      cut = Math.max(0, 1 - k * 1.4);
      alpha = Math.min(1, 0.4 + k);
    } else if (spec.mode === "dissolve") {
      scale = 1 + 0.08 * k;
      cut = k;
      alpha = 1 - k * 0.4;
    } else if (spec.mode === "shrink") {
      scale = Math.max(0.05, 1 - 0.95 * k * k);
      cut = k * 0.6;
      alpha = 1 - k * 0.2;
    }
    frames.push(transformFrame(src, rendered.w, rendered.h, anchor, scale, cut, alpha));
  }
  return { frames: frames, fps: rendered.fps };
}

function boundsOf(frames, w, h) {
  const box = { x0: w, y0: h, x1: -1, y1: -1 };
  for (const rgba of frames) {
    for (let y = 0; y < h; y++) {
      for (let x = 0; x < w; x++) {
        if (rgba[(y * w + x) * 4 + 3] >= AlphaCut) {
          box.x0 = Math.min(box.x0, x);
          box.y0 = Math.min(box.y0, y);
          box.x1 = Math.max(box.x1, x);
          box.y1 = Math.max(box.y1, y);
        }
      }
    }
  }
  if (box.x1 < 0) {
    box.x0 = 0;
    box.y0 = 0;
    box.x1 = w - 1;
    box.y1 = h - 1;
  }
  return box;
}

// 기준점(원본 캔버스 좌표)
function anchorOf(part, box, origin, frames, w, h) {
  let mode = "bbox";
  if (part.anchor !== undefined) {
    mode = part.anchor;
  }
  let point = [(box.x0 + box.x1 + 1) / 2, (box.y0 + box.y1 + 1) / 2];
  if (mode === "origin") {
    point = [origin[0], origin[1]];
  } else if (mode === "axis") {
    // 조준 방향으로 돌리는 베기·찌르기: 캐릭터 자리(원점 x)에서, 그림이 몰린 높이(불투명도 무게중심 y)를 축으로 삼는다.
    // 그래야 돌렸을 때 그림이 조준선 위에 놓인다(허리 기준이면 머리 높이의 칼날 호가 옆으로 비껴 나간다).
    let weight = 0;
    let sumY = 0;
    for (const rgba of frames) {
      for (let y = 0; y < h; y++) {
        for (let x = 0; x < w; x++) {
          const a = rgba[(y * w + x) * 4 + 3];
          if (a >= AlphaCut) {
            weight += a;
            sumY += a * y;
          }
        }
      }
    }
    point = [origin[0], sumY / Math.max(1, weight)];
  } else if (mode === "waist") {
    point = [origin[0], origin[1] - WaistHeight];
  } else if (mode === "left") {
    point = [box.x0, (box.y0 + box.y1 + 1) / 2];
  } else if (mode === "right") {
    point = [box.x1 + 1, (box.y0 + box.y1 + 1) / 2];
  } else if (Array.isArray(mode)) {
    point = [origin[0] + mode[0], origin[1] + mode[1]];
  }
  return [Math.round(point[0]), Math.round(point[1])];
}

// 기준점이 가운데 오도록 잘라낸 프레임들
function cropFrames(part, rendered, info) {
  const { w, h, frames, origin } = rendered;
  let fade = 0;
  if (part.fade !== undefined) {
    fade = part.fade;
  }
  const fadeCount = Math.round(frames.length * fade);
  for (let i = 0; i < fadeCount; i++) {
    const index = frames.length - fadeCount + i;
    multiplyAlpha(frames[index], 1 - (i + 1) / (fadeCount + 1));
  }
  let fadeIn = 0;
  if (part.fadeIn !== undefined) {
    fadeIn = part.fadeIn;
  }
  const fadeInCount = Math.round(frames.length * fadeIn);
  for (let i = 0; i < fadeInCount; i++) {
    multiplyAlpha(frames[i], (i + 1) / (fadeInCount + 1));
  }
  if (part.alpha !== undefined) {
    for (const rgba of frames) {
      multiplyAlpha(rgba, part.alpha);
    }
  }
  const box = boundsOf(frames, w, h);
  let anchor = anchorOf(part, box, origin, frames, w, h);
  let halfW = Math.max(anchor[0] - box.x0, box.x1 + 1 - anchor[0]) + 1;
  let halfH = Math.max(anchor[1] - box.y0, box.y1 + 1 - anchor[1]) + 1;
  // maxHalf: [가로, 세로] 반폭 상한(픽셀). 기준점에서 그보다 먼 그림(긴 꼬리, 멀리 튄 불꽃)은 잘라낸다.
  if (part.maxHalf !== undefined) {
    halfW = Math.min(halfW, part.maxHalf[0]);
    halfH = Math.min(halfH, part.maxHalf[1]);
  }
  // trail: 돌진 길. 그림 왼쪽 끝(이펙트 원점 + dx)에서 스킬 사거리만큼을 정확히 담아 늘리지 않고 그린다.
  if (part.trail && info !== undefined && info.range > 0) {
    halfW = Math.round(info.range * PixelsPerUnit / 2);
    let dx = 0;
    if (part.dx !== undefined) {
      dx = part.dx;
    }
    anchor = [origin[0] + dx + halfW, anchor[1]];
  }
  if (part.square) {
    halfW = Math.max(halfW, halfH);
    halfH = halfW;
  }
  // 가로세로를 4의 배수로 맞춘다(Unity 블록 압축 조건)
  halfW = Math.ceil(halfW / 2) * 2;
  halfH = Math.ceil(halfH / 2) * 2;
  const outW = halfW * 2;
  const outH = halfH * 2;
  const left = anchor[0] - halfW;
  const top = anchor[1] - halfH;
  const out = [];
  for (const rgba of frames) {
    const img = new Uint8ClampedArray(outW * outH * 4);
    for (let y = 0; y < outH; y++) {
      const sy = top + y;
      if (sy >= 0 && sy < h) {
        for (let x = 0; x < outW; x++) {
          const sx = left + x;
          if (sx >= 0 && sx < w) {
            const s = (sy * w + sx) * 4;
            const d = (y * outW + x) * 4;
            img[d] = rgba[s];
            img[d + 1] = rgba[s + 1];
            img[d + 2] = rgba[s + 2];
            img[d + 3] = rgba[s + 3];
            // maxHalf로 자른 가장자리는 8픽셀에 걸쳐 흐리게 해 빛 번짐이 네모나게 끊기지 않게 한다.
            if (part.maxHalf !== undefined) {
              const edge = Math.min(x + 1, outW - x, y + 1, outH - y);
              if (edge < 8) {
                img[d + 3] = Math.round(img[d + 3] * (edge / 8));
              }
            }
          }
        }
      }
    }
    out.push(img);
  }
  let zoom = 1;
  if (part.zoom !== undefined) {
    zoom = part.zoom;
  }
  if (zoom === 1) {
    return { w: outW, h: outH, frames: out, halfW: halfW };
  }
  const big = [];
  for (const img of out) {
    const zw = outW * zoom;
    const z = new Uint8ClampedArray(zw * outH * zoom * 4);
    for (let y = 0; y < outH * zoom; y++) {
      for (let x = 0; x < zw; x++) {
        const s = (Math.floor(y / zoom) * outW + Math.floor(x / zoom)) * 4;
        const d = (y * zw + x) * 4;
        z[d] = img[s];
        z[d + 1] = img[s + 1];
        z[d + 2] = img[s + 2];
        z[d + 3] = img[s + 3];
      }
    }
    big.push(z);
  }
  return { w: outW * zoom, h: outH * zoom, frames: big, halfW: halfW * zoom };
}

// skills.mjs의 bind: 스킬 에셋이 없는 묶음(무기 기본 공격, 몬스터)의 반경 값
const bindOf = new Map();

// 스킬 에셋(Assets/2.Model/Data/Job/<이름>.asset)의 종류·반경
const KindArea = 0;
const KindDash = 1;
const KindBuff = 3;
function readSkill(name) {
  const path = join(projectRoot, "Assets", "2.Model", "Data", "Job", name + ".asset");
  const skill = { kind: -1, radius: 0, explosionRadius: 0, range: 0 };
  if (existsSync(path)) {
    for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
      const match = line.match(/^  (kind|radius|explosionRadius|range): ([-0-9.]+)\s*$/);
      if (match) {
        skill[match[1]] = Number(match[2]);
      }
    }
  }
  return skill;
}

// 부위별 크기 값. 게임은 그림 가로를 (반경 x 배율 x 2)로 늘리므로,
// 배율 = 그림 반폭(유닛) / 스킬 반경 으로 두면 그림 픽셀이 늘거나 줄지 않는다.
// coverage = 그림 반폭 / 스킬 반경 (1이면 그림 끝이 판정 끝에 닿는다)
function sizeValue(name, partName, cropped, part) {
  const skill = readSkill(name);
  Object.assign(skill, bindOf.get(name) ?? {});
  let zoom = 1;
  if (part.zoom !== undefined) {
    zoom = part.zoom;
  }
  const halfUnits = cropped.halfW / PixelsPerUnit;
  let bound = 0;
  let key = "";
  if (partName === "cast") {
    if (skill.kind === KindDash || skill.kind < 0) {
      key = "cast_size";
    } else if (skill.kind === KindBuff) {
      key = "cast_scale";
      bound = Math.max(0.9, skill.radius);
    } else {
      key = "cast_scale";
      bound = skill.radius;
    }
  } else if (partName === "impact") {
    if (skill.explosionRadius > 0) {
      key = "impact_scale";
      bound = skill.explosionRadius;
    } else {
      key = "impact_size";
    }
  } else if (partName === "muzzle") {
    key = "muzzle_size";
  } else if (partName === "trail") {
    return { key: "trail_width", value: cropped.h / PixelsPerUnit, zoom: zoom, note: "  length " + (cropped.w / PixelsPerUnit).toFixed(2) + " (range " + skill.range + ")" };
  } else if (partName === "projectile") {
    return { key: "proj_size", value: cropped.w / PixelsPerUnit, zoom: zoom, note: "" };
  }
  if (bound > 0) {
    return { key: key, value: halfUnits / bound, zoom: zoom, note: "  coverage " + Math.round((halfUnits / bound) * 100) + "% of r=" + bound };
  }
  return { key: key, value: halfUnits, zoom: zoom, note: "" };
}

function framePng(w, h, rgba) {
  const img = canvas(w, h, [0, 0, 0, 0]);
  img.data.set(rgba);
  return img.png();
}

function writeMeta(folder, values) {
  const path = join(folder, "meta.txt");
  const lines = [];
  const seen = new Set();
  if (existsSync(path)) {
    for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
      const equals = line.indexOf("=");
      if (equals > 0) {
        const key = line.slice(0, equals).trim();
        if (key in values) {
          lines.push(key + "=" + values[key]);
          seen.add(key);
        } else {
          lines.push(line);
        }
      }
    }
  }
  for (const key of Object.keys(values)) {
    if (!seen.has(key)) {
      lines.push(key + "=" + values[key]);
    }
  }
  writeFileSync(path, lines.join("\n") + "\n");
}

// 이번에 쓴 개수보다 번호가 큰 예전 프레임(.png, .meta)을 지운다. 같은 번호는 덮어써서 .meta(GUID)를 유지한다.
function removeExtraFrames(folder, partName, count) {
  for (const file of readdirSync(folder)) {
    const match = file.match(/^([a-z]+)_(\d\d)\.png(\.meta)?$/);
    if (match && match[1] === partName && Number(match[2]) >= count) {
      unlinkSync(join(folder, file));
    }
  }
}

const PartFpsKey = { cast: "cast_fps", trail: "trail_fps", projectile: "projectile_fps", impact: "impact_fps", muzzle: "muzzle_fps" };
const GroundKey = { cast: "cast_ground", impact: "impact_ground" };

// 확인용 그림: 부위마다 한 줄, 프레임을 어두운 바닥 위에 늘어놓는다.
function previewSheet(title, rows) {
  const pad = 6;
  const label = 14;
  let width = 200;
  let height = 24;
  for (const row of rows) {
    const shown = Math.min(row.frames.length, 10);
    width = Math.max(width, pad + shown * (row.w + pad));
    height += label + row.h + pad;
  }
  const img = canvas(width, height, [14, 12, 18, 255]);
  img.text(title, pad, 6, 2);
  let y = 24;
  for (const row of rows) {
    img.text(row.label, pad, y, 1, [170, 164, 190]);
    y += label;
    const shown = Math.min(row.frames.length, 10);
    for (let i = 0; i < shown; i++) {
      const index = Math.round((i * (row.frames.length - 1)) / Math.max(1, shown - 1));
      const x = pad + i * (row.w + pad);
      img.rect(x, y, row.w, row.h, PreviewBg);
      img.paste(row.frames[index], row.w, row.h, x, y);
    }
    y += row.h + pad;
  }
  return img.png();
}

// 원본 이펙트 여러 개를 한 장에 5개씩 늘어놓는다(프레임 10장, 구간 전체).
async function survey(ids) {
  const perSheet = 5;
  for (let start = 0; start < ids.length; start += perSheet) {
    const rows = [];
    for (const id of ids.slice(start, start + perSheet)) {
      const part = { id: id, frames: 10, anchor: "bbox" };
      const rendered = await renderPart(part);
      const cropped = cropFrames(part, rendered);
      const duration = rendered.fx.meta.duration;
      rows.push({ label: id + "  " + duration + "s  step " + (duration / 10).toFixed(2) + "s", w: cropped.w, h: cropped.h, frames: cropped.frames });
    }
    const file = join(previewRoot, "survey", "sheet_" + String(start / perSheet).padStart(2, "0") + ".png");
    save(file, previewSheet("survey", rows));
    console.log(file);
  }
}

async function exportSkills() {
  const config = (await import(pathToFileURL(join(here, "skills.mjs")).href)).default;
  let only = null;
  if (flags.only) {
    only = new Set(flags.only.split(","));
  }
  const groups = new Map();
  const report = [];
  for (const skill of config) {
    if (skill.bind !== undefined) {
      bindOf.set(skill.name, skill.bind);
    }
  }
  for (const skill of config) {
    if (only === null || only.has(skill.name)) {
      const folder = join(vfxRoot, skill.name);
      const rows = [];
      const metaValues = { ...(skill.meta ?? {}) };
      let wroteIntro = false;
      let wroteOutro = false;
      for (const [partName, part] of Object.entries(skill.parts)) {
        const rendered = await renderPart(part);
        // 장판의 나타나는/사라지는 그림: 반복 그림과 한꺼번에 잘라 칸 크기와 기준점을 맞춘 뒤 다시 나눈다.
        let intro = { frames: [], fps: 0 };
        let outro = { frames: [], fps: 0 };
        let cropPart = part;
        if (part.intro !== undefined || part.outro !== undefined) {
          const anchor = anchorOf(part, boundsOf(rendered.frames, rendered.w, rendered.h), rendered.origin, rendered.frames, rendered.w, rendered.h);
          intro = await sideFrames(part, part.intro, rendered, anchor, true);
          outro = await sideFrames(part, part.outro, rendered, anchor, false);
          rendered.frames = [...intro.frames, ...rendered.frames, ...outro.frames];
          cropPart = { ...part, fade: 0, fadeIn: 0 };
        }
        const all = cropFrames(cropPart, rendered, { ...readSkill(skill.name), ...(skill.bind ?? {}) });
        const loopCount = all.frames.length - intro.frames.length - outro.frames.length;
        const cropped = { ...all, frames: all.frames.slice(intro.frames.length, intro.frames.length + loopCount) };
        const sides = [];
        if (intro.frames.length > 0) {
          sides.push({ name: "intro", fps: intro.fps, frames: all.frames.slice(0, intro.frames.length) });
          wroteIntro = true;
        }
        if (outro.frames.length > 0) {
          sides.push({ name: "outro", fps: outro.fps, frames: all.frames.slice(intro.frames.length + loopCount) });
          wroteOutro = true;
        }
        for (const side of sides) {
          metaValues[side.name + "_fps"] = side.fps.toFixed(1);
          rows.push({ label: side.name + "  " + part.id + "  " + side.frames.length + "f @" + side.fps.toFixed(1), w: cropped.w, h: cropped.h, frames: side.frames });
          if (!flags["preview-only"]) {
            mkdirSync(folder, { recursive: true });
            for (let i = 0; i < side.frames.length; i++) {
              writeFileSync(join(folder, side.name + "_" + String(i).padStart(2, "0") + ".png"), framePng(cropped.w, cropped.h, side.frames[i]));
            }
            removeExtraFrames(folder, side.name, side.frames.length);
          }
        }
        if (partName in GroundKey) {
          let ground = "0";
          if (part.ground) {
            ground = "1";
          }
          metaValues[GroundKey[partName]] = ground;
        }
        metaValues[PartFpsKey[partName]] = rendered.fps.toFixed(1);
        // 크기 값: 그림 픽셀이 언제나 32픽셀 = 1유닛(zoom이면 그 배수)으로 보이도록 스킬 반경에서 거꾸로 계산한다.
        const sizing = sizeValue(skill.name, partName, cropped, part);
        metaValues[sizing.key] = sizing.value.toFixed(3);
        report.push(skill.name + "." + partName + " " + cropped.w + "x" + cropped.h + " zoom " + sizing.zoom + " -> " + sizing.key + "=" + sizing.value.toFixed(2) + sizing.note);
        rows.push({ label: partName + "  " + part.id + "  " + cropped.frames.length + "f @" + rendered.fps.toFixed(1) + "  " + cropped.w + "x" + cropped.h, w: cropped.w, h: cropped.h, frames: cropped.frames });
        if (!flags["preview-only"]) {
          mkdirSync(folder, { recursive: true });
          for (let i = 0; i < cropped.frames.length; i++) {
            const name = partName + "_" + String(i).padStart(2, "0") + ".png";
            writeFileSync(join(folder, name), framePng(cropped.w, cropped.h, cropped.frames[i]));
          }
          removeExtraFrames(folder, partName, cropped.frames.length);
        }
      }
      // 대응표에서 빠진 부위의 예전 프레임은 지운다(keep에 적은 부위는 둔다).
      if (!flags["preview-only"] && existsSync(folder)) {
        if (!wroteIntro) {
          removeExtraFrames(folder, "intro", 0);
        }
        if (!wroteOutro) {
          removeExtraFrames(folder, "outro", 0);
        }
        for (const partName of Object.keys(PartFpsKey)) {
          if (!(partName in skill.parts) && !(skill.keep ?? []).includes(partName)) {
            removeExtraFrames(folder, partName, 0);
          }
        }
      }
      if (!flags["preview-only"]) {
        writeMeta(folder, metaValues);
      }
      let group = skill.group;
      if (group === undefined) {
        group = "etc";
      }
      if (!groups.has(group)) {
        groups.set(group, []);
      }
      for (const row of rows) {
        row.label = skill.name + "  " + row.label;
        groups.get(group).push(row);
      }
      console.log(skill.name + ": " + rows.map((r) => r.label.split("  ")[1]).join(", "));
    }
  }
  console.log(report.join("\n"));
  for (const [group, rows] of groups) {
    const file = join(previewRoot, group + ".png");
    save(file, previewSheet(group, rows));
    console.log(file);
  }
}

if (flags.survey) {
  await survey(flags.survey.split(","));
} else {
  await exportSkills();
}
