import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import meshes
def load(path):
    tris, slots = meshes.load_fbx(path)
    (x0, y0, z0), (x1, y1, z1) = meshes.bounds(tris)
    s = 1.8 / (y1 - y0)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    # FBX is right-handed, facing +X with right hand at +Z; Unity: facing +Z, right +X (a mirror, as any RH->LH import).
    def f(p): return ((p[2] - cz) * s, (p[1] - y0) * s, (p[0] - cx) * s)
    return meshes.transform(tris, f), slots
