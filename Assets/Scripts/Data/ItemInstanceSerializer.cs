using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// <see cref="ItemInstance"/> 를 세이브 스트림에 싣고 내리는 곳.
///
/// 기록 형식은 [타입 태그 문자열] + (태그가 비지 않았으면) [바이트 길이] + [페이로드] 다.
/// 길이를 앞에 두는 덕에 <b>모르는 타입은 통째로 건너뛸 수 있다</b> — 나중에 인스턴스 종류를
/// 추가하거나 없애도 세이브 파일 전체가 깨지지 않는다.
///
/// 타입 등록은 Assembly-CSharp 를 한 번 훑어 자동으로 한다(파생 클래스를 만들기만 하면 된다).
/// </summary>
public static class ItemInstanceSerializer
{
    private static Dictionary<string, System.Type> types;

    private static void EnsureTypes()
    {
        if (types != null) return;

        types = new Dictionary<string, System.Type>();

        // 게임 코드는 전부 ItemInstance 와 같은 어셈블리(Assembly-CSharp)에 있으므로 그것만 훑는다.
        foreach (System.Type type in typeof(ItemInstance).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ItemInstance).IsAssignableFrom(type)) continue;
            if (type.GetConstructor(System.Type.EmptyTypes) == null)
            {
                Debug.LogError($"[ItemInstanceSerializer] '{type.Name}' 에 기본 생성자가 없어 세이브에서 복원할 수 없습니다.");
                continue;
            }

            ItemInstance probe = (ItemInstance)System.Activator.CreateInstance(type);
            string typeId = probe.TypeId;
            if (string.IsNullOrEmpty(typeId))
            {
                Debug.LogError($"[ItemInstanceSerializer] '{type.Name}' 의 TypeId 가 비어 있습니다.");
                continue;
            }
            if (types.TryGetValue(typeId, out System.Type existing))
            {
                Debug.LogError($"[ItemInstanceSerializer] TypeId '{typeId}' 가 '{existing.Name}' 과 '{type.Name}' 에서 중복됩니다.");
                continue;
            }
            types[typeId] = type;
        }
    }

    /// <summary>인스턴스를 기록한다. null 이면 빈 태그 하나만 쓴다.</summary>
    public static void Write(BinaryWriter writer, ItemInstance instance)
    {
        if (instance == null || string.IsNullOrEmpty(instance.TypeId))
        {
            writer.Write("");
            return;
        }

        byte[] payload = ToBytes(instance);
        writer.Write(instance.TypeId);
        writer.Write(payload.Length);
        writer.Write(payload);
    }

    /// <summary>인스턴스를 읽는다. 모르는 타입이면 페이로드를 건너뛰고 null 을 돌려준다.</summary>
    public static ItemInstance Read(BinaryReader reader)
    {
        string typeId = reader.ReadString();
        if (string.IsNullOrEmpty(typeId)) return null;

        int length = reader.ReadInt32();
        byte[] payload = reader.ReadBytes(length);
        return FromBytes(typeId, payload);
    }

    /// <summary>페이로드만 바이트 배열로 뽑는다(길이를 먼저 알아야 하므로 메모리에 한 번 담는다).</summary>
    public static byte[] ToBytes(ItemInstance instance)
    {
        if (instance == null) return System.Array.Empty<byte>();

        using MemoryStream buffer = new();
        using (BinaryWriter payload = new(buffer))
        {
            instance.Write(payload);
            payload.Flush();
        }
        return buffer.ToArray();
    }

    /// <summary>타입 태그 + 페이로드로 인스턴스를 복원한다. 실패하면 null.</summary>
    public static ItemInstance FromBytes(string typeId, byte[] payload)
    {
        if (string.IsNullOrEmpty(typeId)) return null;

        EnsureTypes();
        if (!types.TryGetValue(typeId, out System.Type type))
        {
            Debug.LogWarning($"[ItemInstanceSerializer] 모르는 인스턴스 타입 '{typeId}' 을 건너뜁니다.");
            return null;
        }

        try
        {
            ItemInstance instance = (ItemInstance)System.Activator.CreateInstance(type);
            using MemoryStream buffer = new(payload ?? System.Array.Empty<byte>());
            using BinaryReader reader = new(buffer);
            instance.Read(reader);
            return instance;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ItemInstanceSerializer] '{typeId}' 복원 실패, 무시합니다: {e.Message}");
            return null;
        }
    }
}
