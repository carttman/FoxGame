using UnityEngine;

// 4x4 맵 배치 데이터 (개발계획서 4.2)
public static class LevelLayout
{
    public const float TileSize = 2f;

    // 위(z=3)부터 아래(z=0) 순서, 왼쪽이 x=0
    // S=시작  .=바닥  H=구멍  I=아이템(바닥 위)  *=구멍 위 공중 아이템
    public static readonly string[] Rows =
    {
        "I.HI",
        ".*..",
        "...I",
        "S.HI",
    };

    public static int Width => Rows[0].Length;
    public static int Depth => Rows.Length;

    public static char Cell(int x, int z) => Rows[Depth - 1 - z][x];

    public static bool IsHole(int x, int z)
    {
        char c = Cell(x, z);
        return c == 'H' || c == '*';
    }

    // 맵 중심이 월드 원점, 타일 윗면이 y=0
    public static Vector3 TileCenter(int x, int z) =>
        new Vector3((x - (Width - 1) * 0.5f) * TileSize, 0f, (z - (Depth - 1) * 0.5f) * TileSize);

    public static Vector3 StartPosition()
    {
        for (int z = 0; z < Depth; z++)
            for (int x = 0; x < Width; x++)
                if (Cell(x, z) == 'S') return TileCenter(x, z);
        return Vector3.zero;
    }
}
