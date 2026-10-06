# Minimal binary FBX (7.x) reader: returns a node tree of (name, props, children).
import struct, zlib, sys

class Node:
    def __init__(self, name, props, children):
        self.name, self.props, self.children = name, props, children
    def find(self, name):
        for c in self.children:
            if c.name == name: return c
        return None
    def all(self, name):
        return [c for c in self.children if c.name == name]
    def __repr__(self): return "Node(%s, %d props, %d children)" % (self.name, len(self.props), len(self.children))

def _read_prop(data, pos):
    t = chr(data[pos]); pos += 1
    if t == 'Y': return struct.unpack_from('<h', data, pos)[0], pos + 2
    if t == 'C': return data[pos] != 0, pos + 1
    if t == 'I': return struct.unpack_from('<i', data, pos)[0], pos + 4
    if t == 'F': return struct.unpack_from('<f', data, pos)[0], pos + 4
    if t == 'D': return struct.unpack_from('<d', data, pos)[0], pos + 8
    if t == 'L': return struct.unpack_from('<q', data, pos)[0], pos + 8
    if t in 'SR':
        n = struct.unpack_from('<I', data, pos)[0]; pos += 4
        raw = data[pos:pos + n]; pos += n
        return (raw.decode('utf-8', 'replace') if t == 'S' else raw), pos
    if t in 'fdlib':
        count, enc, clen = struct.unpack_from('<III', data, pos); pos += 12
        raw = data[pos:pos + clen]; pos += clen
        if enc == 1: raw = zlib.decompress(raw)
        fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': 'B'}[t]
        return list(struct.unpack('<%d%s' % (count, fmt), raw)), pos
    raise ValueError('unknown prop type %r at %d' % (t, pos))

def _read_node(data, pos, version):
    if version >= 7500:
        end, nprops, plen = struct.unpack_from('<QQQ', data, pos); pos += 24
    else:
        end, nprops, plen = struct.unpack_from('<III', data, pos); pos += 12
    nlen = data[pos]; pos += 1
    if end == 0: return None, pos
    name = data[pos:pos + nlen].decode('ascii', 'replace'); pos += nlen
    props = []
    for _ in range(nprops):
        v, pos = _read_prop(data, pos); props.append(v)
    children = []
    sentinel = 25 if version >= 7500 else 13
    while pos < end - sentinel:
        child, pos = _read_node(data, pos, version)
        if child is None: break
        children.append(child)
    return Node(name, props, children), end

def load(path):
    data = open(path, 'rb').read()
    if not data.startswith(b'Kaydara FBX Binary'): raise ValueError('not binary FBX')
    version = struct.unpack_from('<I', data, 23)[0]
    pos = 27
    roots = []
    while pos < len(data) - 160:
        node, pos = _read_node(data, pos, version)
        if node is None: break
        roots.append(node)
    return Node('root', [], roots), version

def props70(node):
    out = {}
    p = node.find('Properties70')
    if p is None: return out
    for c in p.children:
        if c.name == 'P' and c.props: out[c.props[0]] = c.props[4:]
    return out
