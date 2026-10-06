# Mirrors ProceduralAnimator.Reach / PoseSoldierArms in Python to check the hold before Unity.
import math, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import render
from swmread import read
def sub(a, b): return tuple(a[i] - b[i] for i in range(3))
def add(a, b): return tuple(a[i] + b[i] for i in range(3))
def mul(a, s): return tuple(x * s for x in a)
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a): l = math.sqrt(dot(a, a)); return mul(a, 1 / l) if l > 1e-9 else a
def length(a): return math.sqrt(dot(a, a))
def from_to(a, b):
    a, b = norm(a), norm(b)
    v = cross(a, b); c = dot(a, b)
    if c < -0.9999: return [[-1, 0, 0], [0, -1, 0], [0, 0, 1]]
    k = 1 / (1 + c)
    return [[v[0] * v[0] * k + c, v[1] * v[0] * k - v[2], v[2] * v[0] * k + v[1]],
            [v[0] * v[1] * k + v[2], v[1] * v[1] * k + c, v[2] * v[1] * k - v[0]],
            [v[0] * v[2] * k - v[1], v[1] * v[2] * k + v[0], v[2] * v[2] * k + c]]
def ap(m, p): return tuple(sum(m[i][k] * p[k] for k in range(3)) for i in range(3))
def mm(a, b): return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def tr(m): return [[m[j][i] for j in range(3)] for i in range(3)]
I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
M = sys.argv[1]; gun_id = sys.argv[3]
slots, parts = read(os.path.join(M, 'soldier.bytes'))
gslots, gparts = read(os.path.join(M, gun_id + '.bytes'))
P = {k: v[0] for k, v in parts.items()}
hold = (0.08, 1.22, 0.18); gs = 1.25
gun_len = gparts['muzzle'][0][2]
def gun_point(p): return add(hold, mul(p, gs))
def reach(side_name, target, side):
    shoulder = P['arm' + side_name]
    fore_local = sub(P['fore' + side_name], P['arm' + side_name])
    hand_local = sub(P['hand' + side_name], P['fore' + side_name])
    l1, l2 = length(fore_local), length(hand_local)
    to = sub(target, shoulder)
    dist = max(abs(l1 - l2) + 0.01, min(length(to), l1 + l2 - 0.005))
    d = norm(to)
    hint = (side * 0.6, -1.0, -0.2)
    bend = sub(hint, mul(d, dot(hint, d))); bend = norm(bend)
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist); h = math.sqrt(max(0, l1 * l1 - a * a))
    elbow = add(add(shoulder, mul(d, a)), mul(bend, h))
    hand = add(shoulder, mul(d, dist))
    Ru = from_to(fore_local, sub(elbow, shoulder))
    elbow_now = add(shoulder, ap(Ru, fore_local))
    Rf = from_to(hand_local, ap(tr(Ru), sub(hand, elbow_now)))
    miss = length(sub(add(elbow_now, ap(mm(Ru, Rf), hand_local)), target))
    print('%s arm: l1 %.3f l2 %.3f target dist %.3f -> hand misses target by %.3f m' % (side_name, l1, l2, length(to), miss))
    return Ru, Rf
pistol = gun_len < 0.35
Rr = reach('R', gun_point((0, -0.045, -0.02)), 1)
support = (-0.01, -0.06, 0.02) if pistol else (0, -0.025, min(0.24, gun_len * 0.45))
Rl = reach('L', gun_point(support), -1)
tris, all_slots = [], list(slots)
for name, (pivot, ts) in parts.items():
    if not ts: continue
    for a, b, c, s in ts:
        def place(q):
            if name in ('armL', 'armR'):
                Ru = (Rl if name == 'armL' else Rr)[0]
                return add(pivot, ap(Ru, q))
            if name in ('foreL', 'foreR'):
                side = name[-1]; Ru, Rf = (Rl if side == 'L' else Rr)
                sh = P['arm' + side]
                elbow = add(sh, ap(Ru, sub(P['fore' + side], P['arm' + side])))
                return add(elbow, ap(mm(Ru, Rf), q))
            return add(pivot, q)
        tris.append((place(a), place(b), place(c), s))
base = len(all_slots); all_slots += gslots
for a, b, c, s in gparts['gun'][1]:
    tris.append((gun_point(a), gun_point(b), gun_point(c), base + s))
out = sys.argv[2]
render.render(tris, all_slots, out + '_34.png', 400, yaw=-145, pitch=10)
render.render(tris, all_slots, out + '_side.png', 400, yaw=-90, pitch=0)
render.render(tris, all_slots, out + '_top.png', 400, yaw=180, pitch=75)
