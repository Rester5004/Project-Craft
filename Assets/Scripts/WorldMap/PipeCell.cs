using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파이프가 나르고 있는 짐 하나. 청크의 <see cref="PlaceableRecord"/> 에 실려 세이브에 남는다.
///
/// 남은 시간을 <b>절대 시각이 아니라 잔여(초)로</b> 들고 있는 이유:
/// Time.time 은 세션마다 0부터 다시 시작하고, 언로드된 청크에서도 시간이 흐르면
/// "기계는 안 도는데 파이프만 도는" 모순이 생긴다.
/// </summary>
public class ParcelRecord
{
    public string itemName;
    public int count;
    public ItemInstance instance;

    /// <summary>도착할 기계의 월드 셀.</summary>
    public int destX, destY;

    /// <summary>남은 이동 시간(초).</summary>
    public float remaining;

    public Vector2Int Destination => new Vector2Int(destX, destY);
}

/// <summary>
/// 로드된 파이프 한 칸의 런타임 상태.
///
/// <b>MonoBehaviour 가 아니다.</b> 파이프는 수백 개가 깔리므로 칸마다 GameObject 를 만들면
/// Update 가 수백 개 돌게 된다 — <see cref="PipeNetworkManager"/> 하나가 대신 돌본다.
/// </summary>
public class PipeCell
{
    public Vector2Int cell;
    public PipeBlock block;
    public PlaceableRecord record;

    /// <summary>4방향 연결 마스크(N=1, E=2, S=4, W=8). 파생 상태라 저장하지 않고 매번 계산한다.</summary>
    public byte mask;

    /// <summary>싣고 있는 짐(없으면 null). 한 칸에 하나만 싣는다.</summary>
    public ParcelRecord parcel;

    /// <summary>이 경로 캐시를 계산한 시점의 TopologyVersion. 값이 다르면 다시 찾는다.</summary>
    public int routeVersion = -1;

    /// <summary>이동 시간 순으로 정렬된 도착 후보. 하나만 캐시하면 그 기계가 찰 때마다 재탐색하게 된다.</summary>
    public readonly List<PipeRouter.Sink> sinks = new List<PipeRouter.Sink>();

    /// <summary>이 시각 전에는 추출을 다시 시도하지 않는다(받아 줄 곳이 없을 때 매 프레임 훑지 않도록).</summary>
    public float nextAttemptTime;

    public PipeCell(Vector2Int cell, PipeBlock block, PlaceableRecord record)
    {
        this.cell = cell;
        this.block = block;
        this.record = record;
        LoadFrom(record);
    }

    /// <summary>레코드 → 런타임 상태.</summary>
    public void LoadFrom(PlaceableRecord source)
    {
        record = source;
        parcel = source != null && source.parcels != null && source.parcels.Length > 0
            ? source.parcels[0]
            : null;
    }

    /// <summary>런타임 상태 → 레코드. 짐이 바뀔 때마다 즉시 부른다(청크 언로드·저장 사이에 잃지 않도록).</summary>
    public void WriteBack()
    {
        if (record == null) return;
        record.parcels = parcel != null
            ? new ParcelRecord[] { parcel }
            : System.Array.Empty<ParcelRecord>();
    }
}
