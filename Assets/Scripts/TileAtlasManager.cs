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

    // 아틀라스 이미지의 전체 세로가 8칸(0~7)일 때, 
    // 왼쪽 아래를 (0,0)으로 삼는 유니티 좌표계 기준으로 완벽 변환된 조견표입니다.
    private readonly Dictionary<int, Vector2Int> atlasCoordinates = new Dictionary<int, Vector2Int>()
    {
        { 0,  new Vector2Int(0, 2) }, // Isolated_1x1         (원래 0, 5 -> 7 - 5 = 2)
        { 1,  new Vector2Int(2, 3) }, // Dead_End_N            (원래 2, 4 -> 7 - 4 = 3)
        { 2,  new Vector2Int(0, 5) }, // Dead_End_E            (원래 0, 2 -> 7 - 2 = 5)
        { 3,  new Vector2Int(2, 7) }, // Dead_End_S            (원래 2, 0 -> 7 - 0 = 7)
        { 4,  new Vector2Int(4, 5) }, // Dead_End_W            (원래 4, 2 -> 7 - 2 = 5)
        { 5,  new Vector2Int(0, 0) }, // Horizontal_Bridge     (원래 0, 7 -> 7 - 7 = 0)
        { 6,  new Vector2Int(1, 0) }, // Vertical_Bridge       (원래 1, 7 -> 7 - 7 = 0)
        { 7,  new Vector2Int(1, 3) }, // L_Corner_NE           (원래 1, 4 -> 7 - 4 = 3)
        { 8,  new Vector2Int(1, 7) }, // L_Corner_SE           (원래 1, 0 -> 7 - 0 = 7)
        { 9,  new Vector2Int(3, 7) }, // L_Corner_SW           (원래 3, 0 -> 7 - 0 = 7)
        { 10, new Vector2Int(3, 3) }, // L_Corner_NW           (원래 3, 4 -> 7 - 4 = 3)
        { 11, new Vector2Int(0, 3) }, // L_Corner_NE_Filled    (원래 0, 4 -> 7 - 4 = 3)
        { 12, new Vector2Int(0, 7) }, // L_Corner_SE_Filled    (원래 0, 0 -> 7 - 0 = 7)
        { 13, new Vector2Int(4, 7) }, // L_Corner_SW_Filled    (원래 4, 0 -> 7 - 0 = 7)
        { 14, new Vector2Int(4, 3) }, // L_Corner_NW_Filled    (원래 4, 4 -> 7 - 4 = 3)
        { 15, new Vector2Int(1, 5) }, // T_Junction_E_Open     (원래 1, 2 -> 7 - 2 = 5)
        { 16, new Vector2Int(2, 6) }, // T_Junction_S_Open     (원래 2, 1 -> 7 - 1 = 6)
        { 17, new Vector2Int(3, 5) }, // T_Junction_W_Open     (원래 3, 2 -> 7 - 2 = 5)
        { 18, new Vector2Int(2, 4) }, // T_Junction_N_Open     (원래 2, 3 -> 7 - 3 = 4)
        { 19, new Vector2Int(0, 4) }, // T_Junction_E_Half_Top (원래 0, 3 -> 7 - 3 = 4)
        { 20, new Vector2Int(0, 6) }, // T_Junction_E_Half_Bot (원래 0, 1 -> 7 - 1 = 6)
        { 21, new Vector2Int(7, 7) }, // T_Junction_S_Half_R   (원래 7, 0 -> 7 - 0 = 7)
        { 22, new Vector2Int(7, 3) }, // T_Junction_S_Half_L   (원래 7, 4 -> 7 - 4 = 3)
        { 23, new Vector2Int(4, 6) }, // T_Junction_W_Half_Bot (원래 4, 1 -> 7 - 1 = 6)
        { 24, new Vector2Int(4, 4) }, // T_Junction_W_Half_Top (원래 4, 3 -> 7 - 3 = 4)
        { 25, new Vector2Int(1, 4) }, // T_Junction_N_Half_R   (원래 1, 3 -> 7 - 3 = 4)
        { 26, new Vector2Int(3, 4) }, // T_Junction_N_Half_L   (원래 3, 3 -> 7 - 3 = 4)
        { 27, new Vector2Int(5, 2) }, // T_Junction_E_Solid    (원래 5, 5 -> 7 - 5 = 2)
        { 28, new Vector2Int(5, 3) }, // T_Junction_S_Solid    (원래 5, 4 -> 7 - 4 = 3)
        { 29, new Vector2Int(6, 1) }, // T_Junction_W_Solid    (원래 6, 6 -> 7 - 6 = 1)
        { 30, new Vector2Int(4, 0) }, // T_Junction_N_Solid    (원래 4, 7 -> 7 - 7 = 0)
        { 31, new Vector2Int(2, 5) }, // Cross_Junction_Open   (원래 2, 2 -> 7 - 2 = 5)
        { 32, new Vector2Int(1, 2) }, // Cross_Var_NE_Filled   (원래 1, 5 -> 7 - 5 = 2)
        { 33, new Vector2Int(1, 6) }, // Cross_Var_SE_Filled   (원래 1, 1 -> 7 - 1 = 6)
        { 34, new Vector2Int(3, 6) }, // Cross_Var_SW_Filled   (원래 3, 1 -> 7 - 1 = 6)
        { 35, new Vector2Int(3, 2) }, // Cross_Var_NW_Filled   (원래 3, 5 -> 7 - 5 = 2)
        { 36, new Vector2Int(0, 1) }, // Cross_Var_Right_Fill  (원래 0, 6 -> 7 - 6 = 1)
        { 37, new Vector2Int(2, 1) }, // Cross_Var_Bottom_Fill (원래 2, 6 -> 7 - 6 = 1)
        { 38, new Vector2Int(4, 1) }, // Cross_Var_Left_Filled (원래 4, 6 -> 7 - 6 = 1)
        { 39, new Vector2Int(2, 0) }, // Cross_Var_Top_Filled  (원래 2, 7 -> 7 - 7 = 0)
        { 40, new Vector2Int(5, 6) }, // Cross_Var_Diagonal_1  (원래 5, 1 -> 7 - 1 = 6)
        { 41, new Vector2Int(5, 4) }, // Cross_Var_Diagonal_2  (원래 5, 3 -> 7 - 3 = 4)
        { 42, new Vector2Int(5, 5) }, // Cross_Var_Three_Fill1 (원래 5, 2 -> 7 - 2 = 5)
        { 43, new Vector2Int(6, 6) }, // Cross_Var_Three_Fill2 (원래 6, 1 -> 7 - 1 = 6)
        { 44, new Vector2Int(6, 4) }, // Cross_Var_Three_Fill3 (원래 6, 3 -> 7 - 3 = 4)
        { 45, new Vector2Int(6, 5) }, // Cross_Var_Three_Fill4 (원래 6, 2 -> 7 - 2 = 5)
        { 46, new Vector2Int(2, 2) }  // Full_Solid_Wall       (원래 2, 5 -> 7 - 5 = 2)
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