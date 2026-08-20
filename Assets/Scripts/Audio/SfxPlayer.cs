using UnityEngine;

/// <summary>
/// 이름 붙은 <b>단발 효과음</b>을 재생하는 한 곳. 지금은 조합·완성음 하나뿐이다.
///
/// 왜 한 곳에 모으나: 재생을 부르는 쪽이 여럿(<see cref="CraftingTableUI"/> · <see cref="MachineInstance"/>)이라,
/// 볼륨이나 최소 간격 같은 규칙을 나중에 바꿀 때 <b>여기만 고치면 호출부가 안 바뀐다</b>.
///
/// <b>루프 사운드는 여기서 다루지 않는다</b> — 발소리는 걷는 동안 계속 돌아야 해서
/// 자기 <c>AudioSource</c> 를 켜고 끄는 <see cref="PlayerFootsteps"/> 가 따로 맡는다.
///
/// 오브젝트는 <c>GameRig</c> 프리팹에 저작한다(개수가 언제나 정확히 하나다).
/// <c>PersistAcrossScenes</c> 가 false 인 것은 rig 안에 살기 때문이다 — <see cref="UIManager"/> 와 같다.
/// </summary>
public class SfxPlayer : Singleton<SfxPlayer>
{
    protected override bool PersistAcrossScenes => false;

    [Tooltip("단발 재생용. loop 0 · playOnAwake 0 · spatialBlend 0(2D) 으로 프리팹에 저작한다.")]
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioSource backgroundSource;

    [Header("클립")]
    [Tooltip("조합대에서 조합에 성공했을 때, 그리고 UI가 열린 기계가 하나를 완성했을 때.")]
    [SerializeField] private AudioClip craftSound;
    [SerializeField] private AudioClip miningSound;
    [SerializeField] private AudioClip backgroundSound;

    [SerializeField, Range(0f, 1f)] private float craftVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float miningVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float backgroundVolume = 1f;

    private bool isMingingSoundPlaying = false;

    /// <summary>
    /// 조합·완성음. 재생기가 아직 없으면(씬 로드 전·에디트 모드) <b>조용히 아무 일도 하지 않는다.</b>
    ///
    /// ⚠ <b>여기서 <c>Instance</c> 를 쓰면 안 된다.</b> 게터는 못 찾으면 빈 <c>GameObject</c> 를
    /// 만들어 버리는데, 그렇게 생긴 재생기는 클립도 <c>AudioSource</c> 도 없어서
    /// <b>영영 무음인 유령</b>이 되고, 진짜 재생기가 나타나도 그쪽이 중복으로 지워진다.
    /// <c>InstanceIfAlive</c> 는 찾지도 만들지도 않는다.
    /// </summary>
    public static void PlayCraft()
    {
        SfxPlayer player = InstanceIfAlive;
        if (player != null) player.source.loop = false;
        if (player != null) player.Play(player.craftSound, player.craftVolume);
    }
    public static void PlayMining()
    {
        SfxPlayer player = InstanceIfAlive;
        if(player.isMingingSoundPlaying) return;
        if (player != null) player.source.loop = true;
        if (player != null) player.Play(player.miningSound, player.miningVolume);
        player.isMingingSoundPlaying = true;
    }
    public static void StopMining()
    {
        SfxPlayer player = InstanceIfAlive;
        if (player != null) player.source.loop = false;
        if (player != null) player.isMingingSoundPlaying = false;
        if (player != null) player.source.Stop();
    }
    private void Play(AudioClip clip, float volume)
    {
        if (source == null || clip == null) return;
        source.PlayOneShot(clip, volume);
    }
    protected override void Awake()
    {
        base.Awake();
        if (backgroundSource != null && backgroundSound != null)
        {
            backgroundSource.loop = true;
            backgroundSource.clip = backgroundSound;
            backgroundSource.volume = backgroundVolume;
            backgroundSource.Play();
        }
    }
}
