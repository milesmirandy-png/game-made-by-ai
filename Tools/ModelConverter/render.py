# Tiny flat-shaded z-buffer rasteriser: renders triangle lists to PNG (via ImageMagick).
import math, subprocess

def _srgb(c): return max(0, min(255, int(255 * (c if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055) if False else 255 * c)))

def render(tris, slots, path, size=360, yaw=30.0, pitch=15.0, bg=(40, 44, 52), view=None):
    # camera: rotate model by yaw (around Y) then pitch (around X); orthographic, looking down -Z
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    cp, sp = math.cos(math.radians(pitch)), math.sin(math.radians(pitch))
    def xf(p):
        x, y, z = p
        x, z = cy * x + sy * z, -sy * x + cy * z
        y, z = cp * y - sp * z, sp * y + cp * z
        return (x, y, z)
    tt = [(xf(a), xf(b), xf(c), s) for a, b, c, s in tris]
    xs = [p[i][0] for p in tt for i in range(3)]; ys = [p[i][1] for p in tt for i in range(3)]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    if view: minx, maxx, miny, maxy = view
    span = max(maxx - minx, maxy - miny) * 1.08 or 1
    cx, cyy = (minx + maxx) / 2, (miny + maxy) / 2
    W = H = size
    zbuf = [-1e30] * (W * H)
    img = bytearray(bytes(bg) * (W * H))
    light = (0.35, 0.8, 0.5); ln = math.sqrt(sum(l * l for l in light)); light = tuple(l / ln for l in light)
    def scr(p): return ((p[0] - cx) / span * W + W / 2, H / 2 - (p[1] - cyy) / span * H, p[2])
    for a, b, c, s in tt:
        ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
        vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
        nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
        nl = math.sqrt(nx * nx + ny * ny + nz * nz) or 1
        nx, ny, nz = nx / nl, ny / nl, nz / nl
        if nz < 0: nx, ny, nz = -nx, -ny, -nz   # two-sided
        shade = 0.45 + 0.55 * max(0.0, nx * light[0] + ny * light[1] + nz * light[2])
        col = slots[s][1]
        rgb = tuple(max(0, min(255, int(255 * min(1.0, ch) ** (1 / 2.2) * shade))) for ch in col)
        A, B, C = scr(a), scr(b), scr(c)
        x0 = max(0, int(min(A[0], B[0], C[0]))); x1 = min(W - 1, int(max(A[0], B[0], C[0])) + 1)
        y0 = max(0, int(min(A[1], B[1], C[1]))); y1 = min(H - 1, int(max(A[1], B[1], C[1])) + 1)
        den = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(den) < 1e-12: continue
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                px, py = x + 0.5, y + 0.5
                w0 = ((B[1] - C[1]) * (px - C[0]) + (C[0] - B[0]) * (py - C[1])) / den
                w1 = ((C[1] - A[1]) * (px - C[0]) + (A[0] - C[0]) * (py - C[1])) / den
                w2 = 1 - w0 - w1
                if w0 < -1e-6 or w1 < -1e-6 or w2 < -1e-6: continue
                z = w0 * A[2] + w1 * B[2] + w2 * C[2]
                o = y * W + x
                if z > zbuf[o]:
                    zbuf[o] = z
                    img[o * 3:o * 3 + 3] = bytes(rgb)
    ppm = b'P6 %d %d 255\n' % (W, H) + bytes(img)
    subprocess.run(['convert', 'ppm:-', path], input=ppm, check=True)
