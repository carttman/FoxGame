# 복셀 여우 캐릭터 생성 스크립트
# 실행: blender -b -P make_fox.py
# 결과: fox_voxel.blend, fox_voxel.fbx, fox_preview.png (이 스크립트와 같은 폴더)
#
# 부위(몸통/머리/다리4/꼬리)를 별도 오브젝트로 만들고 관절 위치에 피벗을 둔다.
# Unity에서 리깅 없이 각 부위 Transform을 회전시켜 walk/jump 애니메이션을 만들 수 있다.

import os
import sys

import bpy

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.append(OUT_DIR)
from voxel_utils import box, build_part, export_fbx, make_materials, make_root, render_preview, reset_scene  # noqa: E402

S = 0.05  # 복셀 1칸 크기(m)

COLORS = {
    "Orange": (0.95, 0.45, 0.10, 1.0),
    "White":  (0.97, 0.95, 0.90, 1.0),
    "Black":  (0.06, 0.05, 0.05, 1.0),
    "Brown":  (0.30, 0.15, 0.08, 1.0),
}

# 좌표계: Blender 기준 -Y가 앞, Z가 위 (FBX 내보내기 후 Unity에서 +Z가 앞)
PARTS = {}

body = {}
box(body, -2, 1, -4, 3, 3, 5, "Orange")
box(body, -1, 0, -4, 3, 3, 3, "White")      # 배
box(body, -2, 1, -4, -4, 3, 4, "White")     # 가슴
PARTS["Body"] = (body, (0, 0, 4))

head = {}
box(head, -3, 2, -8, -5, 5, 9, "Orange")
box(head, -3, 2, -8, -8, 5, 6, "White")     # 볼
box(head, -1, 0, -10, -9, 5, 6, "White")    # 주둥이
box(head, -1, 0, -10, -10, 6, 6, "Black")   # 코
head[(-2, -8, 7)] = "Black"                 # 눈
head[(1, -8, 7)] = "Black"
for ex in (-3, 1):                          # 귀
    box(head, ex, ex + 1, -6, -5, 10, 10, "Orange")
    head[(ex + 1 if ex < 0 else ex, -6, 10)] = "Brown"  # 귀 안쪽
    box(head, ex, ex + 1, -6, -6, 11, 11, "Black")
PARTS["Head"] = (head, (0, -5, 5))

# -Y를 바라보는 여우에게 +X가 왼쪽이다
for name, lx, ly in (("Leg_FR", -2, -4), ("Leg_FL", 0, -4),
                     ("Leg_BR", -2, 2), ("Leg_BL", 0, 2)):
    leg = {}
    box(leg, lx, lx + 1, ly, ly + 1, 1, 2, "Orange")
    box(leg, lx, lx + 1, ly, ly + 1, 0, 0, "Black")
    PARTS[name] = (leg, (lx + 1, ly + 1, 3))

tail = {}
box(tail, -1, 0, 4, 5, 4, 5, "Orange")
box(tail, -2, 1, 6, 8, 5, 7, "Orange")
box(tail, -2, 1, 9, 10, 5, 7, "White")
PARTS["Tail"] = (tail, (0, 4, 5))


def main():
    reset_scene()
    mats = make_materials("Fox", COLORS)

    root = make_root("Fox")
    for name, (vox, pivot) in PARTS.items():
        build_part(name, vox, pivot, mats, root, S)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "fox_voxel.blend"))
    export_fbx(root, os.path.join(OUT_DIR, "fox_voxel.fbx"))
    render_preview(os.path.join(OUT_DIR, "fox_preview.png"), target=(0.0, 0.0, 0.3), ortho_scale=1.5)

    total = sum(len(v) for v, _ in PARTS.values())
    print(f"[make_fox] 완료: 부위 {len(PARTS)}개, 복셀 {total}개")


main()
