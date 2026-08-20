using System.IO;
using UnityEngine;

/// <summary>
/// 플레이어 상태(위치 · 인벤토리 · 선택 슬롯)를 디스크에 저장/복원한다.
/// 플레이어 오브젝트에 부착하며, <see cref="WorldMap"/> 과 같은 규약
/// (persistentDataPath, magic + version 헤더)을 따른다.
///
/// 실행 순서를 뒤로 미뤄(DefaultExecutionOrder) Inventory/ItemDictionary 초기화와
/// TestItemGiver 의 지급이 끝난 뒤에 로드가 적용되도록 한다.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerSave : MonoBehaviour
{
    // v2: 슬롯마다 ItemInstance(커스텀 도구의 재질·내구도)를 덧붙였다. v1 도 계속 읽는다.
    private const int SaveMagic = 0x50435031; // 'PCP1'
    private const int SaveVersion = 2;

    public static string DefaultSavePath =>
        Path.Combine(Application.persistentDataPath, "player.dat");

    private string savePath;
    private bool loaded;   // 로드 전 저장으로 기존 파일을 덮어쓰지 않도록 하는 가드
    private Rigidbody2D body;

    /// <summary>
    /// <b>종료 시점에 Inventory.Instance 를 부르면 늦다.</b> Inventory 가 먼저 OnApplicationQuit 을 받으면
    /// 그 뒤로 Instance 는 null 을 돌려주고, Save 가 "저장할 게 없다"로 오해해 조용히 빠져나간다
    /// — 마지막 자동 저장 이후의 인벤토리가 통째로 날아갔다. 그래서 참조를 미리 붙들어 둔다.
    /// </summary>
    private Inventory inventory;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        savePath = DefaultSavePath;
    }

    private void Start()
    {
        inventory = Inventory.Instance;
        Load();
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave += Save; // 월드가 저장될 때 플레이어도 함께 저장
    }

    private void OnDestroy()
    {
        // 파괴 경로에서 Instance 를 부르면 종료 중엔 null 이고, 에디트 모드에선 유령 오브젝트를 만든다.
        WorldMap map = WorldMap.InstanceIfAlive;
        if (map != null) map.OnBeforeSave -= Save;
    }

    private void OnApplicationQuit() => Save();

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) Save();
    }

    /// <summary>현재 플레이어 상태를 파일로 기록한다.</summary>
    public void Save()
    {
        if (!loaded) return;

        // Start 에서 붙들어 둔 참조를 쓴다. 여기서 Inventory.Instance 를 부르면 종료 시점에 null 이다.
        if (inventory == null || inventory.slots == null) return;

        // 임시 파일에 다 쓴 뒤에야 교체한다(WorldMap 과 같은 규약).
        SafeFile.WriteAtomic(savePath, writer =>
        {
            writer.Write(SaveMagic);
            writer.Write(SaveVersion);

            // <b>지하 좌표를 쓰면 안 된다.</b> 지하맵은 저장되지 않으므로 다음 실행에는 그 방이 없고,
            // 좌표만 남으면 암반 한가운데(혹은 허공)에서 시작한다. 대신 내려가기 직전의 지상 자리를 쓴다.
            // 인벤토리는 지하에서 주운 것까지 그대로 저장한다 — 들고 나온 것과 같으니 그것이 맞다.
            Vector2 position = UndergroundSession.IsActive
                ? UndergroundSession.SurfaceReturnPosition
                : (Vector2)transform.position;
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(inventory.SelectedSlotIndex);

            writer.Write(inventory.slots.Count);
            foreach (ItemStack stack in inventory.slots)
            {
                bool has = stack.item != null && stack.count > 0;
                writer.Write(has ? stack.item.itemName : "");
                writer.Write(has ? stack.count : 0);
                ItemInstanceSerializer.Write(writer, has ? stack.instance : null);
            }
        });
    }

    /// <summary>저장 파일이 있으면 플레이어 상태를 복원한다.</summary>
    public void Load()
    {
        loaded = true; // 파일이 없어도 이후 저장은 허용

        // 지하에서는 아무것도 복원하지 않는다. 좌표를 복원하면 방 밖(지상 자리)으로 순간이동하고,
        // 인벤토리를 복원하면 살아 있는 Inventory 싱글톤이 들고 온 지금 내용을 디스크의 옛 내용으로 덮어쓴다.
        if (UndergroundSession.IsActive) return;

        if (inventory == null) inventory = Inventory.Instance;   // Start 보다 먼저 불릴 경우 대비
        if (inventory == null || inventory.slots == null) return;

        // <b>세이브가 없다 = 처음 시작한다.</b> 무엇을 주는지는 StartingInventory 가 안다.
        // 지하에서는 위에서 이미 되돌아갔으므로 왕복하며 다시 받을 일이 없다.
        if (!File.Exists(savePath)) { StartingInventory.Grant(inventory); return; }

        try
        {
            using BinaryReader reader = new(File.Open(savePath, FileMode.Open));
            if (reader.ReadInt32() != SaveMagic)
                throw new IOException("Unsupported player save format (magic mismatch).");
            int version = reader.ReadInt32();
            if (version < 1 || version > SaveVersion)
                throw new IOException($"Unsupported player save version {version} (expected 1..{SaveVersion}).");

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            int selectedSlotIndex = reader.ReadInt32();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string itemName = reader.ReadString();
                int itemCount = reader.ReadInt32();
                // 스트림 위치를 맞춰야 하므로 슬롯 범위를 벗어나도 읽기는 끝까지 한다.
                ItemInstance instance = version >= 2 ? ItemInstanceSerializer.Read(reader) : null;
                if (i >= inventory.slots.Count) continue;

                ItemStack stack = inventory.slots[i];
                Items item = string.IsNullOrEmpty(itemName) || ItemDictionary.Instance == null
                    ? null
                    : ItemDictionary.Instance.GetItem(itemName);

                bool has = item != null && itemCount > 0;
                stack.item = has ? item : null;
                stack.count = has ? itemCount : 0;
                stack.instance = has ? instance : null;

                if (!has && !string.IsNullOrEmpty(itemName))
                    Debug.LogWarning($"[PlayerSave] 아이템 '{itemName}' 을 찾을 수 없어 슬롯 {i} 를 비웠습니다(딕셔너리 미등록).");
            }

            SetPosition(new Vector2(x, y));
            inventory.SelectSlot(selectedSlotIndex);
            inventory.NotifyChanged();
        }
        catch (System.Exception e)
        {
            // 지우지 않고 옆으로 치운다(WorldMap 과 같은 규약). 포맷 오류인지 일시적 IO 오류인지 구분할 수 없다.
            Debug.LogWarning($"[PlayerSave] 세이브 파일 로드 실패, 새로 시작합니다: {e.Message}");
            SafeFile.Quarantine(savePath);
        }
    }

    /// <summary>Rigidbody2D 이동을 쓰므로 물리 위치까지 함께 맞춘다.</summary>
    private void SetPosition(Vector2 position)
    {
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }
    }
}
