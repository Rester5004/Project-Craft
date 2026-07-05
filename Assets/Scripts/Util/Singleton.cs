using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;

    public static T Instance
    {
        get
        {
            // 애플리케이션이 종료 중일 때 싱글톤을 호출하면 
            // 유니티 씬에 유령(Ghost) 오브젝트가 남을 수 있으므로 예외 처리합니다.
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[Singleton] Instance '{typeof(T)}' already destroyed on application quit. Won't create again.");
                return null;
            }

            lock (_lock)
            {
                if (_instance == null)
                {
                    // 씬에 이미 배치된 해당 컴포넌트가 있는지 먼저 검색
                    _instance = (T)FindFirstObjectByType(typeof(T));

                    // 씬에 없다면 새로 생성
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        _instance = singletonObject.AddComponent<T>();
                        singletonObject.name = typeof(T).ToString() + " (Singleton)";

                        // 씬이 바뀌어도 파괴되지 않도록 설정
                        DontDestroyOnLoad(singletonObject);
                    }
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        // 씬에 이미 인스턴스가 존재하는데 다른 오브젝트가 또 생성되었다면 중복 제거
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        // 씬 전환 등으로 파괴될 때 인스턴스 참조를 해제합니다.
        if (_instance == this)
        {
            _instance = null;
        }
    }
}