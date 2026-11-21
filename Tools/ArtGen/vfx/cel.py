import numpy as np, cv2

def cel(img, levels=4, cut=0.35):
    """셀 셰이딩: 명도를 levels 단계로 끊고, 진한 같은 색 계열 외곽선 + 바깥쪽 은은한 빛 번짐."""
    a = img.astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3]
    solid = (al > cut).astype(np.float32)
    lum = rgb.max(-1, keepdims=True) + 1e-4
    band = np.clip(np.ceil(lum * levels) / levels, 1.0 / levels, 1)
    flat = np.clip(rgb / lum * band, 0, 1)
    k = np.ones((3, 3), np.uint8)
    ring = (cv2.dilate(solid, k) > 0).astype(np.float32) - solid
    # 외곽선 색: 주변 면 색을 아주 어둡게
    w = cv2.blur(solid, (5, 5)) + 1e-4
    near = np.stack([cv2.blur(flat[..., c] * solid, (5, 5)) for c in range(3)], -1) / w[..., None]
    line = near * 0.28 + np.array([0.05, 0.02, 0.06]) * 0.5
    # 빛 번짐: 원래 반투명 부분을 약하게 남긴다
    glow_a = np.clip(al, 0, 0.5) * 0.55 * (1 - solid) * (1 - ring)
    out = np.zeros_like(a)
    out[..., :3] = rgb * (glow_a > 0)[..., None]
    out[..., 3] = glow_a
    out[ring > 0, :3] = line[ring > 0]
    out[ring > 0, 3] = 1
    out[solid > 0, :3] = flat[solid > 0]
    out[solid > 0, 3] = 1
    return (np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8)
