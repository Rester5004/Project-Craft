using System;
using UnityEngine;

/// <summary>배치된 작물의 성장 표시. 성장 판정은 UTC 시각이라 청크 언로드/게임 종료 중에도 진행된다.</summary>
public class CropInstance : MonoBehaviour
{
    public CropBlock Crop { get; private set; }
    public PlaceableRecord Record { get; private set; }
    public Vector2Int WorldCell { get; private set; }
    public bool IsMature => Growth01 >= 1f;
    public float Growth01
    {
        get
        {
            if (Crop == null || Record == null || Crop.growthSeconds <= 0f) return 1f;
            long elapsedTicks = Math.Max(0L, DateTime.UtcNow.Ticks - Record.plantedAtUtcTicks);
            return Mathf.Clamp01((float)(elapsedTicks / (double)TimeSpan.TicksPerSecond) / Crop.growthSeconds);
        }
    }

    private SpriteRenderer spriteRenderer;
    private int shownStage = -1;

    public void Bind(CropBlock crop, PlaceableRecord record, Vector2Int worldCell)
    {
        Crop = crop;
        Record = record;
        WorldCell = worldCell;
        if (Record.plantedAtUtcTicks <= 0L) Record.plantedAtUtcTicks = DateTime.UtcNow.Ticks;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = crop.cropSprite;
        spriteRenderer.sortingOrder = 2;
        RefreshVisual(true);
    }

    private void Update() => RefreshVisual(false);

    private void RefreshVisual(bool force)
    {
        if (Crop == null) return;
        float growth = Growth01;
        int stage = growth >= 1f ? 2 : growth >= 0.5f ? 1 : 0;
        if (!force && stage == shownStage) return;
        shownStage = stage;

        float scale = stage == 0 ? Crop.seedlingScale : stage == 1 ? Crop.growingScale : 1f;
        transform.localScale = Vector3.one * scale;
        if (spriteRenderer != null)
            spriteRenderer.color = stage == 0 ? new Color(0.55f, 0.9f, 0.55f) : stage == 1 ? new Color(0.8f, 1f, 0.8f) : Color.white;
    }
}
