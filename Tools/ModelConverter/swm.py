# Writes the game's small model format (.bytes, read by ModelLibrary.cs):
#   "SWM1", u8 slotCount, slots: (u8 len, name utf8, u8 r, g, b)
#   u8 partCount, parts: (u8 len, name, f32 px, py, pz, f32 step, u32 triCount, tris: (u8 slot, 9 x i16))
# Points are in Unity space (left-handed, Y up, Z forward), metres, stored relative to the part pivot.
import struct
def _volume(tris):
    return sum((a[0] * (b[1] * c[2] - b[2] * c[1]) - a[1] * (b[0] * c[2] - b[2] * c[0]) + a[2] * (b[0] * c[1] - b[1] * c[0])) / 6 for a, b, c, s in tris)

def write(path, slots, parts):
    # Unity (like any renderer here) needs triangles wound so their cross product points outward,
    # i.e. a positive signed volume. Mirroring a model into Unity's axes reverses that, so fix it.
    if _volume([t for name, pivot, tris in parts for t in tris]) < 0:
        parts = [(name, pivot, [(a, c, b, s) for a, b, c, s in tris]) for name, pivot, tris in parts]
    out = bytearray(b'SWM1')
    out += struct.pack('<B', len(slots))
    for name, col in slots:
        nb = name.encode('utf-8')[:255]
        out += struct.pack('<B', len(nb)) + nb + bytes(max(0, min(255, int(round(c * 255)))) for c in col)
    out += struct.pack('<B', len(parts))
    for name, pivot, tris in parts:
        nb = name.encode('utf-8')
        rel = [tuple((p[0] - pivot[0], p[1] - pivot[1], p[2] - pivot[2]) for p in t[:3]) + (t[3],) for t in tris]
        m = max([abs(c) for t in rel for p in t[:3] for c in p] + [1e-4])
        step = m / 32000.0
        out += struct.pack('<B', len(nb)) + nb + struct.pack('<ffff', pivot[0], pivot[1], pivot[2], step) + struct.pack('<I', len(rel))
        for a, b, c, s in rel:
            out += struct.pack('<B', s)
            for p in (a, b, c):
                out += struct.pack('<hhh', *(int(round(v / step)) for v in p))
    open(path, 'wb').write(bytes(out))
    return len(out)
