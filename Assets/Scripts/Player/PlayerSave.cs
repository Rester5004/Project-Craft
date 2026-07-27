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
    private const int SaveMagic = 0x50435031; // 'PCP1'
    private const int SaveVersion = 1;

    public static string DefaultSavePath =>
        Path.Combine(Application.persistentDataPath, "player.dat");

    private string savePath;
    private bool loaded;   // 로드 전 저장으로 기존 파일을 덮어쓰지 않도록 하는 가드
    private Rigidbody2D body;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        savePath = DefaultSavePath;
    }

    private void Start()
    {
        Load();
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave += Save; // 월드가 저장될 때 플레이어도 함께 저장
    }

    private void OnDestroy()
    {
        if (WorldMap.Instance != null)
            WorldMap.Instance.OnBeforeSave -= Save;
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

        Inventory inventory = Inventory.Instance;
        if (inventory == null || inventory.slots == null) return;

        try
        {
            using BinaryWriter writer = new(File.Open(savePath, FileMode.Create));
            writer.Write(SaveMagic);
            writer.Write(SaveVersion);

            Vector3 position = transform.position;
            writer.Write(position.x);
            writer.Write(position.y);
            writer.Write(inventory.SelectedSlotIndex);

            writer.Write(inventory.slots.Count);
            foreach (ItemStack stack in inventory.slots)
            {
                bool has = stack.item != null && stack.count > 0;
                writer.Write(has ? stack.item.itemName : "");
                writer.Write(has ? stack.count : 0);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerSave] 저장 실패: {e.Message}");
        }
    }

    /// <summary>저장 파일이 있으면 플레이어 상태를 복원한다.</summary>
    public void Load()
    {
        loaded = true; // 파일이 없어도 이후 저장은 허용
        if (!File.Exists(savePath)) return;

        Inventory inventory = Inventory.Instance;
        if (inventory == null || inventory.slots == null) return;

        try
        {
            using BinaryReader reader = new(File.Open(savePath, FileMode.Open));
            if (reader.ReadInt32() != SaveMagic)
                throw new IOException("Unsupported player save format (magic mismatch).");
            int version = reader.ReadInt32();
            if (version != SaveVersion)
                throw new IOException($"Unsupported player save version {version} (expected {SaveVersion}).");

            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            int selectedSlotIndex = reader.ReadInt32();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string itemName = reader.ReadString();
                int itemCount = reader.ReadInt32();
                if (i >= inventory.slots.Count) continue;

                ItemStack stack = inventory.slots[i];
                Items item = string.IsNullOrEmpty(itemName) || ItemDictionary.Instance == null
                    ? null
                    : ItemDictionary.Instance.GetItem(itemName);

                bool has = item != null && itemCount > 0;
                stack.item = has ? item : null;
                stack.count = has ? itemCount : 0;

                if (!has && !string.IsNullOrEmpty(itemName))
                    Debug.LogWarning($"[PlayerSave] 아이템 '{itemName}' 을 찾을 수 없어 슬롯 {i} 를 비웠습니다(딕셔너리 미등록).");
            }

            SetPosition(new Vector2(x, y));
            inventory.SelectSlot(selectedSlotIndex);
            inventory.NotifyChanged();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerSave] 세이브 파일 로드 실패, 새로 시작합니다: {e.Message}");
            File.Delete(savePath);
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
