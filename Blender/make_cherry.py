# 복셀 체리(아이템) 생성 스크립트
# 실행: blender -b -P make_cherry.py
# 결과: cherry_voxel.blend, cherry_voxel.fbx, cherry_preview.png
#
# 피벗은 모델 중심에 둔다. Unity에서 제자리 회전(스핀)하기 좋게.

import os
import sys

import bpy

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.append(OUT_DIR)
from voxel_utils import box, build_part, export_fbx, make_materials, make_root, render_preview, reset_scene  # noqa: E402

S = 0.05  # 복셀 1칸 크기(m) → 체리 전체 약 0.45 x 0.55m

COLORS = {
    "Red":       (0.85, 0.06, 0.10, 1.0),
    "DarkRed":   (0.50, 0.02, 0.06, 1.0),
    "Highlight": (1.00, 0.65, 0.65, 1.0),
    "Stem":      (0.22, 0.40, 0.10, 1.0),
    "Leaf":      (0.35, 0.75, 0.20, 1.0),
}


def berry(vox, cx, cy, cz):
    """5x5x5 둥근 열매. 아래쪽은 어둡게, 앞-위에 하이라이트 한 점."""
    for x in range(-2, 3):
        for y in range(-2, 3):
            for z in range(-2, 3):
                if x * x + y * y + z * z <= 5:  # 모서리를 깎아 둥글게
                    vox[(cx + x, cy + y, cz + z)] = "DarkRed" if z == -2 else "Red"
    vox[(cx - 1, cy - 2, cz + 1)] = "Highlight"


def stem(vox, a, b):
    """두 복셀 좌표 사이를 복셀 선으로 잇는다."""
    n = max(abs(b[i] - a[i]) for i in range(3))
    for k in range(n + 1):
        t = k / n if n else 0
        vox[tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))] = "Stem"


def build_voxels():
    vox = {}
    berry(vox, -3, 0, 2)          # 왼쪽 열매
    berry(vox, 3, -1, 1)          # 오른쪽 열매 (조금 낮고 앞쪽)
    top = (0, 0, 10)
    stem(vox, (-3, 0, 5), top)
    stem(vox, (3, -1, 4), top)
    box(vox, 1, 3, 0, 0, 10, 11, "Leaf")  # 잎
    vox[(4, 0, 11)] = "Leaf"
    return vox


def main():
    reset_scene()
    mats = make_materials("Cherry", COLORS)
    vox = build_voxels()

    xs = [p[0] for p in vox]; ys = [p[1] for p in vox]; zs = [p[2] for p in vox]
    center = ((min(xs) + max(xs) + 1) / 2, (min(ys) + max(ys) + 1) / 2, (min(zs) + max(zs) + 1) / 2)

    root = make_root("Cherry")
    build_part("CherryMesh", vox, center, mats, root, S)
    root.children[0].location = (0, 0, 0)  # 루트 = 모델 중심

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "cherry_voxel.blend"))
    export_fbx(root, os.path.join(OUT_DIR, "cherry_voxel.fbx"))
    render_preview(os.path.join(OUT_DIR, "cherry_preview.png"), target=(0.0, 0.0, 0.0), ortho_scale=0.9)

    size = [(max(v) - min(v) + 1) * S for v in (xs, ys, zs)]
    print(f"[make_cherry] 완료: 복셀 {len(vox)}개, 크기 {size[0]:.2f} x {size[1]:.2f} x {size[2]:.2f} m")


main()
