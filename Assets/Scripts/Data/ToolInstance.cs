using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 커스텀 도구 하나의 개체 데이터. 어떤 재질로 조립됐고 내구도가 얼마나 남았는지를 들고 있다.
/// 재질은 <see cref="ToolMaterial"/> 참조가 아니라 <b>ID 문자열</b>로 보관한다 —
/// 세이브에 그대로 실리고, 에셋이 없어져도 세이브가 깨지지 않는다.
/// </summary>
public class ToolInstance : ItemInstance
{
    private const int Format = 1;

    /// <summary>부품 칸 순서대로의 재질 ID(<see cref="ToolDefinition.slots"/> 와 같은 순서).</summary>
    public string[] materialIds = System.Array.Empty<string>();

    public int durability;

    /// <summary>만들어질 때 계산된 최대 내구도. 나중에 밸런스를 고쳐도 기존 도구가 흔들리지 않게 함께 보관한다.</summary>
    public int maxDurability;

    public override string TypeId => "tool";

    public ToolInstance() { }

    public ToolInstance(string[] materialIds, int durability, int maxDurability)
    {
        this.materialIds = materialIds ?? System.Array.Empty<string>();
        this.durability = durability;
        this.maxDurability = maxDurability;
    }

    /// <summary>내구도가 남아 있는가.</summary>
    public bool IsBroken => durability <= 0;

    /// <summary>범위를 벗어나면 빈 문자열.</summary>
    public string MaterialAt(int index)
        => materialIds != null && index >= 0 && index < materialIds.Length ? materialIds[index] : "";

    public override ItemInstance Clone()
        => new ToolInstance((string[])materialIds.Clone(), durability, maxDurability);

    public override bool Matches(ItemInstance other)
    {
        if (other is not ToolInstance tool) return false;
        if (durability != tool.durability || maxDurability != tool.maxDurability) return false;
        if (materialIds.Length != tool.materialIds.Length) return false;

        for (int i = 0; i < materialIds.Length; i++)
            if (materialIds[i] != tool.materialIds[i]) return false;
        return true;
    }

    public override void Write(BinaryWriter writer)
    {
        writer.Write(Format);
        writer.Write(durability);
        writer.Write(maxDurability);
        writer.Write(materialIds.Length);
        for (int i = 0; i < materialIds.Length; i++) writer.Write(materialIds[i] ?? "");
    }

    public override void Read(BinaryReader reader)
    {
        reader.ReadInt32();   // Format — 지금은 1뿐이라 분기하지 않는다
        durability = reader.ReadInt32();
        maxDurability = reader.ReadInt32();

        int count = reader.ReadInt32();
        materialIds = new string[count];
        for (int i = 0; i < count; i++) materialIds[i] = reader.ReadString();
    }

    /// <summary>"곡괭이(철, 금)" — 부품 칸 순서대로 재질 이름을 괄호에 넣는다.</summary>
    public override string DecorateName(Items item)
    {
        string baseName = item != null ? item.DisplayName : "";
        if (materialIds == null || materialIds.Length == 0) return baseName;

        StringBuilder builder = new();
        builder.Append(baseName).Append('(');
        for (int i = 0; i < materialIds.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(MaterialName(materialIds[i]));
        }
        builder.Append(')');
        return builder.ToString();
    }

    public override string TooltipExtra() => $"내구도 {durability} / {maxDurability}";

    /// <summary>자루 · 머리 스프라이트를 부품 칸 순서대로 겹쳐 그린다.</summary>
    public override bool CollectIconLayers(Items item, System.Collections.Generic.List<IconLayer> results)
    {
        ToolDefinition definition = item is ToolItem tool ? tool.definition : null;
        return ToolFactory.CollectLayers(definition, this, results);
    }

    private static string MaterialName(string materialId)
    {
        ToolDictionary dictionary = ToolDictionary.Instance;
        ToolMaterial material = dictionary != null ? dictionary.GetMaterial(materialId) : null;
        return material != null ? material.DisplayName : materialId;
    }
}
