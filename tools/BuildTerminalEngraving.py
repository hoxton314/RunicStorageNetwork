"""Carve the pedestal rune on a build-owned FBX copy; never save the source model."""
import bpy, bmesh, hashlib, json, math, sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parents[1]
source = root / 'model-sources/terminal/RunicStorageTerminal.fbx'
output = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
output.mkdir(parents=True, exist_ok=True)
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
objects = {name: bpy.data.objects[name] for name in ('RST_Stone', 'RST_BaseRune')}
for obj in objects.values():
    world = obj.matrix_world.copy()
    obj.data = obj.data.copy()
    obj.data.transform(world)
    obj.parent = None
    obj.matrix_world.identity()

def unity(point):
    return dict(x=point.x, y=point.z, z=point.y)

source_vertices = [dict(name=obj.name, vertices=[unity(v.co) for v in obj.data.vertices]) for obj in objects.values()]

def islands(mesh):
    neighbors = {v.index: set() for v in mesh.vertices}
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    remaining, result = set(neighbors), []
    while remaining:
        group, stack = set(), [min(remaining)]
        while stack:
            index = stack.pop()
            if index in group:
                continue
            group.add(index)
            stack.extend(neighbors[index] - group)
        remaining -= group
        result.append(group)
    return result

def tree(obj):
    return BVHTree.FromPolygons([v.co for v in obj.data.vertices], [list(p.vertices) for p in obj.data.polygons])

def subset(name, obj, selected):
    indices = sorted(selected)
    mapping = {old: new for new, old in enumerate(indices)}
    faces = [[mapping[i] for i in p.vertices] for p in obj.data.polygons if set(p.vertices) <= selected]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([obj.data.vertices[i].co for i in indices], [], faces)
    mesh.update()
    part = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(part)
    return part

stone, rune = objects['RST_Stone'], objects['RST_BaseRune']
bm = bmesh.new()
bm.from_mesh(stone.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(stone.data)
bm.free()
stone.data.update()
surface_tree = tree(stone)
depth, glow_gap, margin = .004, .0003, .001
vertices, faces, checks = [], [], []
for group in islands(rune.data):
    points = [rune.data.vertices[i].co.copy() for i in sorted(group)]
    assert len(points) == 4, ('unexpected rune stroke', len(points))
    center = sum(points, Vector()) / len(points)
    surface, normal, _, gap = surface_tree.find_nearest(center)
    assert gap < .03, ('rune too far from stone', gap)
    normal.normalize()
    u = (points[1] - points[0]).normalized()
    u = (u - normal * u.dot(normal)).normalized()
    v = normal.cross(u)
    ordered = sorted(points, key=lambda p: math.atan2((p-center).dot(v), (p-center).dot(u)))
    projected = [p - normal * (p-surface).dot(normal) for p in ordered]
    middle = sum(projected, Vector()) / len(projected)
    expanded = [p + (p-middle).normalized() * margin for p in projected]
    start = len(vertices)
    vertices.extend([p - normal * depth for p in expanded])
    vertices.extend([p + normal * .008 for p in expanded])
    faces.extend([(start+3, start+2, start+1, start), (start+4, start+5, start+6, start+7)])
    for k in range(4):
        faces.append((start+k, start+(k+1)%4, start+(k+1)%4+4, start+k+4))
    for index in group:
        point = rune.data.vertices[index].co
        rune.data.vertices[index].co = point - normal * ((point-surface).dot(normal) + depth - glow_gap)
    checks.append((middle, normal.copy()))
assert len(checks) == 4, ('unexpected stroke count', len(checks))
rune.data.update()
cutter_mesh = bpy.data.meshes.new('PedestalRuneCutters')
cutter_mesh.from_pydata(vertices, [], faces)
cutter_mesh.update()
cutter = bpy.data.objects.new('PedestalRuneCutters', cutter_mesh)
bpy.context.scene.collection.objects.link(cutter)
combined_vertices, combined_faces = [], []
changed_blocks = 0
for index, group in enumerate(islands(stone.data)):
    part = subset('PedestalBlock_' + str(index), stone, group)
    part_tree = tree(part)
    if any(part_tree.find_nearest(p)[3] < .001 for p, _ in checks):
        changed_blocks += 1
        bm = bmesh.new()
        bm.from_mesh(part.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.0000001)
        boundary = [e for e in bm.edges if e.is_boundary]
        if boundary:
            bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(part.data)
        bm.free()
        part.data.update()
        bpy.context.view_layer.objects.active = part
        modifier = part.modifiers.new('Recessed pedestal rune', 'BOOLEAN')
        modifier.operation = 'DIFFERENCE'
        modifier.solver = 'EXACT'
        modifier.object = cutter
        modifier.use_self = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        bm = bmesh.new()
        bm.from_mesh(part.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        assert all(e.is_manifold for e in bm.edges), ('non-manifold engraved stone block', index)
        bm.to_mesh(part.data)
        bm.free()
        part.data.update()
    offset = len(combined_vertices)
    combined_vertices.extend(v.co.copy() for v in part.data.vertices)
    combined_faces.extend(tuple(offset+i for i in p.vertices) for p in part.data.polygons)
    bpy.data.objects.remove(part, do_unlink=True)
assert changed_blocks == 1, ('unexpected affected blocks', changed_blocks)
mesh = bpy.data.meshes.new('RST_Stone_Engraved')
mesh.from_pydata(combined_vertices, [], combined_faces)
mesh.update()
stone.data = mesh
engraved_tree = tree(stone)
errors = []
for point, normal in checks:
    hit, _, _, _ = engraved_tree.ray_cast(point+normal*.01, -normal, .04)
    assert hit is not None, 'groove floor missing'
    error = abs((point-hit).dot(normal) - depth)
    assert error < .0002, ('incorrect groove depth', error)
    errors.append(error)

def serialize(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertices, normals = [], []
    for tri in mesh.loop_triangles:
        for loop in (tri.loops[0], tri.loops[2], tri.loops[1]):
            vertices.append(unity(mesh.vertices[mesh.loops[loop].vertex_index].co))
            normals.append(unity(mesh.corner_normals[loop].vector))
    return dict(name=obj.name, vertices=vertices, normals=normals, triangles=list(range(len(vertices))))

items = [serialize(stone), serialize(rune)]
assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash, 'source FBX changed'
result = dict(items=items, source=source_vertices, sourceSHA256=source_hash)
(output/'TerminalEngraving.json').write_text(json.dumps(result), encoding='utf-8')
report = dict(sourceSHA256=source_hash, strokeCount=len(checks), affectedBlocks=changed_blocks,
              depth=depth, glowAboveFloor=glow_gap, maxDepthError=max(errors),
              triangles={item['name']: len(item['triangles'])//3 for item in items})
(output/'TerminalEngravingReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('RSN_TERMINAL_ENGRAVING_SUCCESS', json.dumps(report), flush=True)
