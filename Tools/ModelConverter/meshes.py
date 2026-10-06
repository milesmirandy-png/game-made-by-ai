# Loads FBX (binary 7.x) and GLB models into flat triangle lists:
#   tris: list of (p0, p1, p2, slot) with points as (x, y, z) in metres, Y up
#   slots: list of (name, (r, g, b)) colours, 0..1
import json, math, os, struct, subprocess, tempfile, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx

def _decode_image(raw):
    """Decodes PNG/JPG bytes to (w, h, rows) via ImageMagick (raw RGB)."""
    with tempfile.TemporaryDirectory() as d:
        src = os.path.join(d, 'img')
        open(src, 'wb').write(raw)
        out = subprocess.run(['convert', src, '-depth', '8', 'ppm:-'], capture_output=True, check=True).stdout
    # parse binary PPM (P6)
    parts = []
    i = 0
    while len(parts) < 4:
        while out[i:i+1].isspace(): i += 1
        if out[i:i+1] == b'#':
            while out[i:i+1] != b'\n': i += 1
            continue
        j = i
        while not out[j:j+1].isspace(): j += 1
        parts.append(out[i:j]); i = j
    i += 1
    w, h = int(parts[1]), int(parts[2])
    return w, h, out[i:i + w * h * 3]

def _sample(img, u, v):
    w, h, px = img
    x = min(w - 1, max(0, int(u % 1.0 * w))) if u != 1.0 else w - 1
    y = min(h - 1, max(0, int((1.0 - v % 1.0) * h)))
    o = (y * w + x) * 3
    return (px[o] / 255.0, px[o + 1] / 255.0, px[o + 2] / 255.0)

# ---------------- FBX ----------------

def _euler_matrix(rx, ry, rz):
    # FBX default rotation order XYZ: R = Rz * Ry * Rx (applied to column vectors)
    rx, ry, rz = math.radians(rx), math.radians(ry), math.radians(rz)
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    Rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    Ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    Rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    def mul(a, b): return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    return mul(Rz, mul(Ry, Rx))

def _apply(m, p): return tuple(sum(m[i][k] * p[k] for k in range(3)) for i in range(3))

def load_fbx(path, slot_colors_from_texture=True):
    root, _ = fbx.load(path)
    objs = root.find('Objects')
    by_id = {c.props[0]: c for c in objs.children}
    conns = root.find('Connections')
    children_of = {}
    for c in conns.children:
        if c.name == 'C' and c.props[0] == 'OO':
            children_of.setdefault(c.props[2], []).append(c.props[1])
    tris, slots = [], []
    for model in objs.children:
        if model.name != 'Model' or len(model.props) < 3 or model.props[2] != 'Mesh': continue
        p = fbx.props70(model)
        t = p.get('Lcl Translation', [0, 0, 0])[-3:]
        r = p.get('Lcl Rotation', [0, 0, 0])[-3:]
        s = p.get('Lcl Scaling', [1, 1, 1])[-3:]
        rot = _euler_matrix(*r)
        kids = [by_id[k] for k in children_of.get(model.props[0], []) if k in by_id]
        geo = next(k for k in kids if k.name == 'Geometry')
        mats = [k for k in kids if k.name == 'Material']
        # Texture (palette) per material, if any.
        mat_tex = {}
        for m in mats:
            for tid in children_of.get(m.props[0], []):
                tex = by_id.get(tid)
                if tex is None or tex.name != 'Texture': continue
                for vid in children_of.get(tid, []):
                    video = by_id.get(vid)
                    if video is not None and video.name == 'Video' and video.find('Content') is not None:
                        raw = video.find('Content').props[0]
                        if raw: mat_tex[m.props[0]] = _decode_image(raw)
        base = len(slots)
        for m in mats:
            mp = fbx.props70(m)
            col = tuple(mp.get('DiffuseColor', [0.8, 0.8, 0.8])[-3:])
            slots.append((m.props[1].split('\x00')[0], col))
        verts = geo.find('Vertices').props[0]
        pts = []
        for i in range(0, len(verts), 3):
            v = (verts[i] * s[0], verts[i + 1] * s[1], verts[i + 2] * s[2])
            v = _apply(rot, v)
            pts.append((v[0] + t[0], v[1] + t[1], v[2] + t[2]))
        pvi = geo.find('PolygonVertexIndex').props[0]
        # material per polygon
        lem = geo.find('LayerElementMaterial')
        mat_idx = lem.find('Materials').props[0] if lem is not None else [0]
        mat_mode = lem.find('MappingInformationType').props[0] if lem is not None else 'AllSame'
        # UVs
        uvs = None
        leuv = geo.find('LayerElementUV')
        if leuv is not None:
            uv = leuv.find('UV').props[0]
            uvi = leuv.find('UVIndex').props[0] if leuv.find('UVIndex') is not None else None
            uvs = (uv, uvi, leuv.find('MappingInformationType').props[0])
        poly, corner, polyno = [], 0, 0
        extra_slots = {}
        for raw in pvi:
            idx = raw if raw >= 0 else ~raw
            poly.append((idx, corner)); corner += 1
            if raw < 0:
                mi = mat_idx[0] if mat_mode == 'AllSame' or polyno >= len(mat_idx) else mat_idx[polyno]
                mi = min(mi, len(mats) - 1)
                slot = base + mi
                mat = mats[mi]
                if mat.props[0] in mat_tex and uvs is not None:
                    uv, uvi, mode = uvs
                    us = []
                    for (vi, ci) in poly:
                        k = (uvi[ci] if uvi is not None else ci) if mode == 'ByPolygonVertex' else vi
                        us.append((uv[2 * k], uv[2 * k + 1]))
                    cu = sum(a for a, b in us) / len(us); cv = sum(b for a, b in us) / len(us)
                    col = _sample(mat_tex[mat.props[0]], cu, cv)
                    key = tuple(round(c * 31) for c in col)
                    if key not in extra_slots:
                        extra_slots[key] = len(slots)
                        slots.append(('%s_%d' % (slots[base + mi][0], len(extra_slots)), col))
                    slot = extra_slots[key]
                for k in range(1, len(poly) - 1):
                    tris.append((pts[poly[0][0]], pts[poly[k][0]], pts[poly[k + 1][0]], slot))
                poly = []; polyno += 1
    return tris, slots

# ---------------- GLB ----------------

def _quat_matrix(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]

def _node_matrix(node):
    if 'matrix' in node:
        m = node['matrix']
        return [[m[0], m[4], m[8], m[12]], [m[1], m[5], m[9], m[13]], [m[2], m[6], m[10], m[14]], [0, 0, 0, 1]]
    t = node.get('translation', [0, 0, 0]); r = node.get('rotation', [0, 0, 0, 1]); s = node.get('scale', [1, 1, 1])
    R = _quat_matrix(r)
    return [[R[0][0] * s[0], R[0][1] * s[1], R[0][2] * s[2], t[0]],
            [R[1][0] * s[0], R[1][1] * s[1], R[1][2] * s[2], t[1]],
            [R[2][0] * s[0], R[2][1] * s[1], R[2][2] * s[2], t[2]], [0, 0, 0, 1]]

def _mul4(a, b): return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def load_glb(path):
    data = open(path, 'rb').read()
    assert data[:4] == b'glTF'
    jlen = struct.unpack_from('<I', data, 12)[0]
    doc = json.loads(data[20:20 + jlen])
    off = 20 + jlen
    blen = struct.unpack_from('<I', data, off)[0]
    binary = data[off + 8: off + 8 + blen]
    def view_bytes(vi):
        v = doc['bufferViews'][vi]
        o = v.get('byteOffset', 0)
        return binary[o:o + v['byteLength']], v.get('byteStride')
    def accessor(ai):
        a = doc['accessors'][ai]
        raw, stride = view_bytes(a['bufferView'])
        comp = {5126: ('f', 4), 5123: ('H', 2), 5125: ('I', 4), 5121: ('B', 1), 5122: ('h', 2)}[a['componentType']]
        n = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        stride = stride or comp[1] * n
        base = a.get('byteOffset', 0)
        out = []
        for i in range(a['count']):
            vals = struct.unpack_from('<%d%s' % (n, comp[0]), raw, base + i * stride)
            out.append(vals if n > 1 else vals[0])
        return out
    images = {}
    def texture_image(ti):
        src = doc['textures'][ti]['source']
        if src not in images:
            img = doc['images'][src]
            raw, _ = view_bytes(img['bufferView'])
            images[src] = _decode_image(raw)
        return images[src]
    slots, slot_of = [], {}
    def slot_for(mi, color):
        key = (mi, tuple(round(c * 31) for c in color))
        if key not in slot_of:
            name = doc['materials'][mi].get('name', 'mat%d' % mi) if mi is not None else 'default'
            slot_of[key] = len(slots)
            slots.append((name, color))
        return slot_of[key]
    tris = []
    def visit(ni, parent):
        node = doc['nodes'][ni]
        m = _mul4(parent, _node_matrix(node))
        if 'mesh' in node:
            for prim in doc['meshes'][node['mesh']]['primitives']:
                pos = accessor(prim['attributes']['POSITION'])
                idx = accessor(prim['indices']) if 'indices' in prim else list(range(len(pos)))
                mi = prim.get('material')
                mat = doc['materials'][mi] if mi is not None else {}
                pbr = mat.get('pbrMetallicRoughness', {})
                factor = pbr.get('baseColorFactor', [0.8, 0.8, 0.8, 1])[:3]
                tex = pbr.get('baseColorTexture')
                uv = accessor(prim['attributes']['TEXCOORD_0']) if tex is not None and 'TEXCOORD_0' in prim['attributes'] else None
                img = texture_image(tex['index']) if uv is not None else None
                wp = [tuple(m[r][0] * p[0] + m[r][1] * p[1] + m[r][2] * p[2] + m[r][3] for r in range(3)) for p in pos]
                for k in range(0, len(idx), 3):
                    a, b, c = idx[k], idx[k + 1], idx[k + 2]
                    if img is not None:
                        cu = (uv[a][0] + uv[b][0] + uv[c][0]) / 3; cv = 1.0 - (uv[a][1] + uv[b][1] + uv[c][1]) / 3
                        tc = _sample(img, cu, cv)
                        color = tuple(tc[i] * factor[i] for i in range(3))
                    else:
                        color = tuple(factor)
                    tris.append((wp[a], wp[b], wp[c], slot_for(mi, color)))
        for ch in node.get('children', []): visit(ch, m)
    ident = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    scene = doc['scenes'][doc.get('scene', 0)]
    for ni in scene['nodes']: visit(ni, ident)
    return tris, slots

def bounds(tris):
    xs = [p[i][0] for p in tris for i in range(3)]; ys = [p[i][1] for p in tris for i in range(3)]; zs = [p[i][2] for p in tris for i in range(3)]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

def transform(tris, fn):
    return [(fn(a), fn(b), fn(c), s) for a, b, c, s in tris]
