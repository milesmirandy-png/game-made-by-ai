# One step of Loop subdivision with creases, for a triangle list [(a, b, c, slot), ...].
# Every triangle becomes four and the points are moved to smooth the surface, except along creases:
# open edges, edges between two colour slots (the vest's outline on the shirt), and edges sharper
# than `crease_degrees` (the corners of pouches and boxes). Those stay crisp, so gear keeps its shape
# while faces, limbs and cloth get rounder. Flat shading is kept: it's still low-poly, just denser.
import math

def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def _len(a): return math.sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2])

def subdivide(tris, crease_degrees=40.0, weld=1e-5):
    # Weld positions so neighbouring triangles share points.
    index, points = {}, []
    def vid(p):
        key = (round(p[0] / weld), round(p[1] / weld), round(p[2] / weld))
        if key not in index:
            index[key] = len(points)
            points.append(p)
        return index[key]
    faces = []
    for a, b, c, s in tris:
        i, j, k = vid(a), vid(b), vid(c)
        if i == j or j == k or k == i: continue
        faces.append((i, j, k, s))
    normals = []
    for i, j, k, s in faces:
        n = _cross(_sub(points[j], points[i]), _sub(points[k], points[i]))
        l = _len(n)
        normals.append((n[0] / l, n[1] / l, n[2] / l) if l > 1e-12 else None)
    edges = {}
    for f, (i, j, k, s) in enumerate(faces):
        for a, b, opposite in ((i, j, k), (j, k, i), (k, i, j)):
            edges.setdefault((min(a, b), max(a, b)), []).append((f, opposite))
    cos_limit = math.cos(math.radians(crease_degrees))
    crease = {}
    for e, users in edges.items():
        sharp = len(users) != 2
        if not sharp:
            (f0, _), (f1, _) = users
            n0, n1 = normals[f0], normals[f1]
            sharp = faces[f0][3] != faces[f1][3] or n0 is None or n1 is None or (n0[0] * n1[0] + n0[1] * n1[1] + n0[2] * n1[2]) < cos_limit
        crease[e] = sharp
    # New points on the edges.
    edge_point = {}
    for e, users in edges.items():
        a, b = points[e[0]], points[e[1]]
        if crease[e]:
            p = tuple((a[i] + b[i]) * 0.5 for i in range(3))
        else:
            c, d = points[users[0][1]], points[users[1][1]]
            p = tuple(a[i] * 0.375 + b[i] * 0.375 + c[i] * 0.125 + d[i] * 0.125 for i in range(3))
        edge_point[e] = p
    # Moved original points.
    neighbours = {}
    creased = {}
    for (a, b), sharp in crease.items():
        neighbours.setdefault(a, []).append(b)
        neighbours.setdefault(b, []).append(a)
        if sharp:
            creased.setdefault(a, []).append(b)
            creased.setdefault(b, []).append(a)
    moved = []
    for v, p in enumerate(points):
        ring = neighbours.get(v, [])
        sharp = creased.get(v, [])
        if len(sharp) == 0 and ring:
            n = len(ring)
            beta = (0.625 - (0.375 + 0.25 * math.cos(2 * math.pi / n)) ** 2) / n
            s = [sum(points[u][i] for u in ring) for i in range(3)]
            moved.append(tuple(p[i] * (1 - n * beta) + beta * s[i] for i in range(3)))
        elif len(sharp) == 2:
            c, d = points[sharp[0]], points[sharp[1]]
            moved.append(tuple(p[i] * 0.75 + (c[i] + d[i]) * 0.125 for i in range(3)))
        else:
            moved.append(p)   # a corner: stays put
    out = []
    for i, j, k, s in faces:
        a, b, c = moved[i], moved[j], moved[k]
        ab, bc, ca = edge_point[(min(i, j), max(i, j))], edge_point[(min(j, k), max(j, k))], edge_point[(min(k, i), max(k, i))]
        out += [(a, ab, ca, s), (ab, b, bc, s), (ca, bc, c, s), (ab, bc, ca, s)]
    return out
