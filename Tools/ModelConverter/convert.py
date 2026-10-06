# Converts the imported models in SourceModels/ to the game's mesh files.
#   python3 Tools/ModelConverter/convert.py SourceModels Assets/Resources/SWAT/Models <preview folder>
# Needs Python 3 and ImageMagick (convert, montage). See README.md.
import sys, os, glob, math, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshes, render, swm, soldier_norm
up, outdir, prev = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(outdir, exist_ok=True); os.makedirs(prev, exist_ok=True)

def compact(tris, slots):
    used = sorted({t[3] for t in tris})
    remap = {s: i for i, s in enumerate(used)}
    return [(a, b, c, remap[s]) for a, b, c, s in tris], [slots[s] for s in used]

# ---------- Soldier: six parts on the game's joints ----------
tris, slots = soldier_norm.load(os.path.join(up, 'soldier', 'Low Poly Soilder Character.fbx'))
ELBOW = 1.10
groups = {k: [] for k in ('torso', 'head', 'armL', 'armR', 'foreL', 'foreR', 'legL', 'legR')}
for t in tris:
    c = tuple(sum(p[i] for p in t[:3]) / 3 for i in range(3))
    if c[1] > 1.50 and abs(c[0]) < 0.17: g = 'head'
    elif abs(c[0]) > 0.205 and 0.70 < c[1] < 1.43:
        side = 'L' if c[0] < 0 else 'R'
        g = ('arm' if c[1] > ELBOW else 'fore') + side
    elif c[1] < 0.93: g = 'legL' if c[0] < 0 else 'legR'
    else: g = 'torso'
    groups[g].append(t)
def zc(ts, ymin):
    zs = [p[2] for t in ts for p in t[:3] if p[1] > ymin]
    return sum(zs) / len(zs) if zs else 0.0
def ring(ts, y0, y1):
    ps = [p for t in ts for p in t[:3] if y0 <= p[1] <= y1]
    return tuple(sum(p[i] for p in ps) / len(ps) for i in range(3))
pivots = {'torso': (0, 0, 0), 'head': (0, 1.50, zc(groups['head'], 1.45)), 'legL': (-0.10, 0.92, 0.0), 'legR': (0.10, 0.92, 0.0)}
points = {}
for side, sx in (('L', -1), ('R', 1)):
    arm = groups['arm' + side] + groups['fore' + side]
    top = ring(arm, 1.33, 1.43)
    pivots['arm' + side] = (sx * 0.215, 1.39, top[2])
    elbow = ring(arm, ELBOW - 0.04, ELBOW + 0.04)
    pivots['fore' + side] = (elbow[0], ELBOW, elbow[2])
    points['hand' + side] = ring(arm, 0.76, 0.9)   # middle of the glove: where the hand holds things
for k, v in groups.items(): print('soldier', k, len(v), 'pivot', tuple(round(x, 3) for x in pivots[k]))
for k, v in points.items(): print('soldier', k, tuple(round(x, 3) for x in v))
n = swm.write(os.path.join(outdir, 'soldier.bytes'), slots, [(k, pivots[k], groups[k]) for k in groups] + [(k, v, []) for k, v in points.items()])
print('soldier.bytes', n, 'bytes')
# preview: parts tinted to check the split
tint = {'torso': (0.8, 0.8, 0.8), 'head': (1, 0.4, 0.4), 'armL': (0.4, 0.6, 1), 'armR': (0.4, 1, 0.5), 'foreL': (0.2, 0.3, 0.8), 'foreR': (0.2, 0.7, 0.3), 'legL': (1, 0.85, 0.3), 'legR': (0.8, 0.4, 1)}
ptris, pslots = [], []
for k, ts in groups.items():
    pslots.append((k, tint[k]))
    ptris += [(a, b, c, len(pslots) - 1) for a, b, c, s in ts]
render.render(ptris, pslots, os.path.join(prev, 'soldier_parts.png'), 360, yaw=180, pitch=0)
render.render(tris, slots, os.path.join(prev, 'soldier_front.png'), 360, yaw=180, pitch=0)
render.render(tris, slots, os.path.join(prev, 'soldier_left.png'), 360, yaw=90, pitch=0)

# ---------- Shield ----------
st, ss = meshes.load_fbx(os.path.join(up, 'shield', 'Police_Shield.fbx'))
(x0, y0, z0), (x1, y1, z1) = meshes.bounds(st)
s = 1.05 / (z1 - z0)
cx, cy, cz = (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2
SHIELD_FLIP = float(os.environ.get('SHIELD_FLIP', '1'))
# Upright with the viewport at the top (a half turn about the forward axis from the source), handles on the back (-Z).
def fs(p): return (-(p[0] - cx) * s, -(p[2] - cz) * s, (p[1] - cy) * s)
st = meshes.transform(st, fs)
st, ss = compact(st, ss)
print('shield', len(st), 'tris', len(ss), 'colours', meshes.bounds(st))
swm.write(os.path.join(outdir, 'police_shield.bytes'), ss, [('shield', (0, 0, 0), st)])
render.render(st, ss, os.path.join(prev, 'shield_front.png'), 300, yaw=180, pitch=0)
render.render(st, ss, os.path.join(prev, 'shield_back.png'), 300, yaw=0, pitch=0)
render.render(st, ss, os.path.join(prev, 'shield_side.png'), 300, yaw=90, pitch=0)

# ---------- Guns ----------
# file, model id, length (m), -1 where the automatic muzzle guess is backwards (None = trust the guess)
GUNS = [
    ('Assault Rifle-XGeBGFQxYg', 'gun_ak', 0.92, None),
    ('Assault Rifle-MdbcTe6hH3', 'gun_m4', 0.95, None),
    ('Assault Rifle', 'gun_bullpup', 0.78, None),
    ('Sniper Rifle-ZkwSIy3JOV', 'gun_awp', 1.15, None),
    ('Sniper Rifle', 'gun_bolt', 1.08, None),
    ('Submachine Gun-e66LWULNG4', 'gun_p90', 0.56, -1),
    ('Submachine Gun-thBPAYTK5R', 'gun_uzi', 0.36, None),
    ('Submachine Gun', 'gun_mp5', 0.66, -1),
    ('Submachine Gun-I9pqB6HWIC', 'gun_skorpion', 0.34, None),
    ('Submachine Gun-wnFec6z9ii', 'gun_vector', 0.72, None),
    ('Shotgun-f54ZSBZZ8k', 'gun_pump', 1.0, None),
    ('Shotgun', 'gun_double', 0.95, None),
    ('Pistol', 'gun_pistol', 0.21, None),
    ('Pistol-L1u7KkJzY2', 'gun_pistol2', 0.2, None),
    ('Pistol-M6RRKnpfim', 'gun_heavy', 0.26, None),
    ('Revolver', 'gun_revolver', 0.31, None),
    ('Revolver-E9wrywgP9D', 'gun_snub', 0.22, None),
]
# Guns whose muzzle the guess gets backwards (checked by eye on the preview sheet).
OVERRIDE = {gid: -1 for fname, gid, length, flip in GUNS if flip == -1}
tiles = []
for fname, gid, length, flip in GUNS:
    gt, gs = meshes.load_glb(os.path.join(up, 'weapons', fname + '.glb'))
    (x0, y0, z0), (x1, y1, z1) = meshes.bounds(gt)
    sx, sy, sz = x1 - x0, y1 - y0, z1 - z0
    along_x = sx > sz
    # long axis -> u, up -> y, thin -> w
    def to_uvw(p): return (p[0], p[1], p[2]) if along_x else (p[2], p[1], p[0])
    pts = [to_uvw(p) for t in gt for p in t[:3]]
    u0, u1 = min(p[0] for p in pts), max(p[0] for p in pts)
    v0, v1 = min(p[1] for p in pts), max(p[1] for p in pts)
    # guess: the grip / magazine hangs low near the back, so the muzzle points away from the low parts
    low = [p[0] for p in pts if p[1] < v0 + (v1 - v0) * 0.35]
    guess = 1 if (sum(low) / len(low)) < (u0 + u1) / 2 else -1
    d = guess * OVERRIDE.get(gid, 1)
    sc = length / (u1 - u0)
    # origin: 35% of the length from the back (about where the grip is), vertical middle
    rear = u0 if d > 0 else u1
    ou = rear + d * (u1 - u0) * (0.3 if length < 0.4 else 0.36)
    ov = (v0 + v1) / 2
    w_mid = (min(to_uvw(p)[2] for t in gt for p in t[:3]) + max(to_uvw(p)[2] for t in gt for p in t[:3])) / 2
    # Unity: z forward = d * u, y up, x = thin axis; handedness: a mirror overall (RH glTF -> LH Unity)
    def fg(p):
        u, v, w = to_uvw(p)
        z = (u - ou) * d * sc; y = (v - ov) * sc; x = (w - w_mid) * sc
        # to_uvw swaps axes when not along_x (a mirror); flipping the long axis is another; keep the total a mirror
        mirrors = (0 if along_x else 1) + (1 if d < 0 else 0)
        if mirrors % 2 == 0: x = -x
        return (x, y, z)
    nt = meshes.transform(gt, fg)
    nt, ns = compact(nt, gs)
    b0, b1 = meshes.bounds(nt)
    muzzle = (0.0, (b0[1] + b1[1]) / 2 + 0.0, b1[2])
    # muzzle height: average height of the front 5% of points
    front = [p for t in nt for p in t[:3] if p[2] > b1[2] - (b1[2] - b0[2]) * 0.04]
    if front: muzzle = (0.0, sum(p[1] for p in front) / len(front), b1[2])
    swm.write(os.path.join(outdir, gid + '.bytes'), ns, [('gun', (0, 0, 0), nt), ('muzzle', muzzle, [])])
    t = os.path.join(prev, gid + '.png')
    render.render(nt, ns, t, 220, yaw=-90, pitch=0)   # look from +X: forward (+Z) points right
    subprocess.run(['convert', t, '-gravity', 'south', '-background', '#282c34', '-fill', 'white', '-pointsize', '12', '-splice', '0x16', '-annotate', '+0+2', '%s (%s) %.2fm' % (gid, 'guess' if gid not in OVERRIDE else 'flipped', length), t], check=True)
    tiles.append(t)
    print('%-14s tris %4d colours %d  muzzle z %.2f y %.3f  height %.3f' % (gid, len(nt), len(ns), muzzle[2], muzzle[1], b1[1] - b0[1]))
subprocess.run(['montage'] + tiles + ['-tile', '6x', '-geometry', '+2+2', '-background', '#202020', os.path.join(prev, 'guns_sheet.png')], check=True)
