# 복셀 모델 공용 도구 (make_fox.py, make_cherry.py에서 사용)
# 복셀 사전 {(x, y, z): 색이름} → 보이는 면만 가진 메시, 피벗 = 오브젝트 원점

import math
import bpy

FACES = [  # (이웃 방향, 면의 4개 꼭짓점 오프셋 - 바깥에서 볼 때 반시계)
    ((1, 0, 0),  [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
    ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
    ((0, 1, 0),  [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
    ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
    ((0, 0, 1),  [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
    ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)]),
]


def box(vox, x0, x1, y0, y1, z0, z1, color):
    """정수 범위(양끝 포함)의 복셀을 채운다. 나중에 칠한 색이 우선한다."""
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            for z in range(z0, z1 + 1):
                vox[(x, y, z)] = color


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_materials(prefix, colors):
    mats = {}
    for name, rgba in colors.items():
        m = bpy.data.materials.new(f"{prefix}_{name}")
        m.diffuse_color = rgba
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = rgba
            bsdf.inputs["Roughness"].default_value = 0.8
        mats[name] = m
    return mats


def build_part(name, vox, pivot, mats, parent, voxel_size):
    """보이는 면만 생성하고, 피벗(복셀 좌표)을 오브젝트 원점으로 둔다."""
    color_names = list(mats.keys())
    px, py, pz = pivot
    s = voxel_size
    verts, faces, mat_idx, vmap = [], [], [], {}

    def vid(p):
        if p not in vmap:
            vmap[p] = len(verts)
            verts.append(((p[0] - px) * s, (p[1] - py) * s, (p[2] - pz) * s))
        return vmap[p]

    for (x, y, z), color in vox.items():
        for (dx, dy, dz), corners in FACES:
            if (x + dx, y + dy, z + dz) in vox:
                continue
            faces.append([vid((x + cx, y + cy, z + cz)) for cx, cy, cz in corners])
            mat_idx.append(color_names.index(color))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    for m in color_names:
        mesh.materials.append(mats[m])
    for poly, mi in zip(mesh.polygons, mat_idx):
        poly.material_index = mi
        poly.use_smooth = False
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = (px * s, py * s, pz * s)
    return obj


def make_root(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    return root


def export_fbx(root, path):
    """루트와 자식만 FBX로 내보낸다. Blender -Y 앞 → Unity +Z 앞."""
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        add_leaf_bones=False,
        bake_anim=False,
    )


def render_preview(path, target, ortho_scale, cam_offset=(-1.6, -2.2, 1.6)):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.6, 0.6, 0.6)
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0.0, math.radians(-30))
    scene.collection.objects.link(sun)
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    scene.render.film_transparent = True
    scene.view_settings.view_transform = "Standard"  # 머티리얼 색 그대로 표시

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = tuple(target[i] + cam_offset[i] for i in range(3))
    d = [target[i] - cam.location[i] for i in range(3)]
    yaw = math.atan2(d[1], d[0])
    pitch = math.atan2(d[2], math.hypot(d[0], d[1]))
    cam.rotation_euler = (math.pi / 2 + pitch, 0.0, yaw - math.pi / 2)
    scene.camera = cam

    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
