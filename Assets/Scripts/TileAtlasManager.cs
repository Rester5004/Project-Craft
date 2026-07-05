using System;
using System.Collections.Generic;
using UnityEngine;

public class TileAtlasManager : Singleton<TileAtlasManager>
{
    public static List<Vector2Int> All8Directions = new List<Vector2Int>()
    {
        new Vector2Int(0, 1),   // N
        new Vector2Int(1, 1),   // NE
        new Vector2Int(1, 0),   // E
        new Vector2Int(1, -1),  // SE
        new Vector2Int(0, -1),  // S
        new Vector2Int(-1, -1), // SW
        new Vector2Int(-1, 0),  // W
        new Vector2Int(-1, 1),   // NW
    };

    // 업로드해주신 CSV의 원본 (Atlas_X, Atlas_Y) 데이터에
    // 유니티 좌하단 원점 기준 Y축 반전 공식(7 - Atlas_Y)을 전수 적용한 최종 조견표입니다.
    private readonly Dictionary<int, Vector2Int> atlasCoordinates = new Dictionary<int, Vector2Int>()
    {
        { 0,  new Vector2Int(0, 2) },
        { 1,  new Vector2Int(2, 3) },
        { 2,  new Vector2Int(0, 5) },
        { 3,  new Vector2Int(2, 7) },
        { 4,  new Vector2Int(4, 5) },
        { 5,  new Vector2Int(0, 0) },
        { 6,  new Vector2Int(1, 0) },
        { 7,  new Vector2Int(6, 3) },
        { 8,  new Vector2Int(6, 4) },
        { 9,  new Vector2Int(7, 4) },
        { 10,  new Vector2Int(7, 3) },
        { 11,  new Vector2Int(3, 1) },
        { 12,  new Vector2Int(4, 3) },
        { 13,  new Vector2Int(6, 2) },
        { 14,  new Vector2Int(5, 0) },
        { 15,  new Vector2Int(6, 6) },
        { 16,  new Vector2Int(8, 7) },
        { 17,  new Vector2Int(10, 6) },
        { 18,  new Vector2Int(8, 5) },
        { 19,  new Vector2Int(6, 7) },
        { 20,  new Vector2Int(6, 5) },
        { 21,  new Vector2Int(7, 7) },
        { 22,  new Vector2Int(9, 7) },
        { 23,  new Vector2Int(10, 7) },
        { 24,  new Vector2Int(10, 5) },
        { 25,  new Vector2Int(7, 5) },
        { 26,  new Vector2Int(9, 5) },
        { 27,  new Vector2Int(3, 2) },
        { 28,  new Vector2Int(5, 3) },
        { 29,  new Vector2Int(6, 1) },
        { 30,  new Vector2Int(4, 0) },
        { 31,  new Vector2Int(2, 5) },
        { 32,  new Vector2Int(1, 4) },
        { 33,  new Vector2Int(1, 6) },
        { 34,  new Vector2Int(3, 6) },
        { 35,  new Vector2Int(3, 4) },
        { 36,  new Vector2Int(1, 5) },
        { 37,  new Vector2Int(2, 6) },
        { 38,  new Vector2Int(3, 5) },
        { 39,  new Vector2Int(2, 4) },
        { 40,  new Vector2Int(0, 1) },
        { 41,  new Vector2Int(1, 1) },
        { 42,  new Vector2Int(5, 2) },
        { 43,  new Vector2Int(5, 1) },
        { 44,  new Vector2Int(4, 1) },
        { 45,  new Vector2Int(4, 2) },
        { 46,  new Vector2Int(1, 2) },
    };

    private readonly int[] bitmaskToTileIdTable = new int[256];

    protected override void Awake()
    {
        base.Awake();
        InitializeLookUpTable();
    }

    public Vector2Int GetAtlasCoordinate(byte bitmask)
    {
        int tileId = bitmaskToTileIdTable[bitmask];
        if (atlasCoordinates.TryGetValue(tileId, out Vector2Int coord))
        {
            return coord;
        }

        return new Vector2Int(-1, -1); // 유효하지 않은 경우
    }

    private void InitializeLookUpTable()
    {
        for (int i = 0; i < 256; i++)
        {
            bitmaskToTileIdTable[i] = EvaluateTileId((byte)i);
        }
    }

    private int EvaluateTileId(byte bitmask)
    {
        bool n = (bitmask & 1) != 0;
        bool ne = (bitmask & 2) != 0;
        bool e = (bitmask & 4) != 0;
        bool se = (bitmask & 8) != 0;
        bool s = (bitmask & 16) != 0;
        bool sw = (bitmask & 32) != 0;
        bool w = (bitmask & 64) != 0;
        bool nw = (bitmask & 128) != 0;

        string nwC = (!w && !n) ? "outer" : (w && n) ? (nw ? "dark" : "inner") : "outer";
        string neC = (!n && !e) ? "outer" : (n && e) ? (ne ? "dark" : "inner") : "outer";
        string seC = (!e && !s) ? "outer" : (e && s) ? (se ? "dark" : "inner") : "outer";
        string swC = (!s && !w) ? "outer" : (s && w) ? (sw ? "dark" : "inner") : "outer";

        if (!n && !e && !s && !w) return 0;
        if (n && !e && !s && !w) return 1;
        if (!n && e && !s && !w) return 2;
        if (!n && !e && s && !w) return 3;
        if (!n && !e && !s && w) return 4;
        if (!n && e && !s && w) return 5;
        if (n && !e && s && !w) return 6;

        if (n && e && !s && !w) return neC == "inner" ? 7 : 11;
        if (!n && e && s && !w) return seC == "inner" ? 8 : 12;
        if (!n && !e && s && w) return swC == "inner" ? 9 : 13;
        if (n && !e && !s && w) return nwC == "inner" ? 10 : 14;

        if (n && e && s && !w)
        {
            if (neC == "inner" && seC == "inner") return 15;
            if (neC == "dark" && seC == "inner") return 19;
            if (neC == "inner" && seC == "dark") return 20;
            return 27;
        }
        if (!n && e && s && w)
        {
            if (seC == "inner" && swC == "inner") return 16;
            if (seC == "dark" && swC == "inner") return 21; // 7, 0에 완벽 매칭
            if (seC == "inner" && swC == "dark") return 22;
            return 28;
        }
        if (n && !e && s && w)
        {
            if (swC == "inner" && nwC == "inner") return 17;
            if (swC == "inner" && nwC == "dark") return 23;
            if (swC == "dark" && nwC == "inner") return 24;
            return 29;
        }
        if (n && e && !s && w)
        {
            if (nwC == "inner" && neC == "inner") return 18;
            if (nwC == "inner" && neC == "dark") return 25;
            if (nwC == "dark" && neC == "inner") return 26;
            return 30;
        }

        if (n && e && s && w)
        {
            int darkCount = 0;
            if (nwC == "dark") darkCount++;
            if (neC == "dark") darkCount++;
            if (seC == "dark") darkCount++;
            if (swC == "dark") darkCount++;

            if (darkCount == 0) return 31;
            if (darkCount == 1)
            {
                if (neC == "dark") return 32;
                if (seC == "dark") return 33;
                if (swC == "dark") return 34;
                return 35;
            }
            if (darkCount == 2)
            {
                if (neC == "dark" && seC == "dark") return 36;
                if (seC == "dark" && swC == "dark") return 37;
                if (swC == "dark" && nwC == "dark") return 38;
                if (nwC == "dark" && neC == "dark") return 39;
                if (neC == "dark" && swC == "dark") return 40;
                return 41;
            }
            if (darkCount == 3)
            {
                if (neC == "inner") return 42;
                if (seC == "inner") return 43;
                if (swC == "inner") return 44;
                return 45;
            }
            return 46;
        }

        return -1;
    }
}