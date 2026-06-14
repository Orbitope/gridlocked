using UnityEngine;

/// <summary>
/// Persistent SFX singleton. Place one on a Bootstrap GameObject in the
/// MainMenu scene; it survives scene loads via DontDestroyOnLoad, so both
/// games share it. Assign the CC0 clips in the Inspector (see
/// Research/sound_effects.md for sourcing).
///
/// All call sites should guard on AudioManager.Instance != null so scenes
/// opened directly (without going through MainMenu) never NRE.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips (CC0 — see Research/sound_effects.md)")]
    [SerializeField] private AudioClip slide;
    [SerializeField] private AudioClip thock;
    [SerializeField] private AudioClip success;
    [SerializeField] private AudioClip select;

    [Header("Mix")]
    [Range(0f, 1f)] public float volume = 0.8f;

    private AudioSource _src;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _src = GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
    }

    /// <param name="pitch">Slight pitch scaling, e.g. by slide distance.</param>
    public void PlaySlide(float pitch = 1f) => Play(slide, pitch);

    public void PlayThock()   => Play(thock, Random.Range(0.94f, 1.06f));
    public void PlaySuccess() => Play(success, 1f);
    public void PlaySelect()  => Play(select, Random.Range(0.98f, 1.02f));

    private void Play(AudioClip clip, float pitch)
    {
        if (clip == null || _src == null) return;
        _src.pitch = pitch;
        _src.PlayOneShot(clip, volume);
    }
}
