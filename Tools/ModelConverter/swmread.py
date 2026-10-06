import struct
def read(path):
    d = open(path, 'rb').read(); pos = 4
    n = d[pos]; pos += 1
    slots = []
    for _ in range(n):
        l = d[pos]; pos += 1; name = d[pos:pos + l].decode(); pos += l
        slots.append((name, tuple(c / 255 for c in d[pos:pos + 3]))); pos += 3
    parts = {}
    pc = d[pos]; pos += 1
    for _ in range(pc):
        l = d[pos]; pos += 1; name = d[pos:pos + l].decode(); pos += l
        px, py, pz, step = struct.unpack_from('<ffff', d, pos); pos += 16
        tc = struct.unpack_from('<I', d, pos)[0]; pos += 4
        tris = []
        for _ in range(tc):
            s = d[pos]; pos += 1
            v = struct.unpack_from('<9h', d, pos); pos += 18
            tris.append(((v[0] * step, v[1] * step, v[2] * step), (v[3] * step, v[4] * step, v[5] * step), (v[6] * step, v[7] * step, v[8] * step), s))
        parts[name] = ((px, py, pz), tris)
    return slots, parts
