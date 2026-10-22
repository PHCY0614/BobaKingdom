using UnityEngine;

public class AudioController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void SetDefaultVolume() { AudioListener.volume = 0.7f; }

    public AudioSource audioSource;
    public AudioClip bgmIntro, bgmGhostNormal, bgmGhostScared, bgmGhostDead;
    private UIManager uiManager;
    private GhostManager ghostManager;
    void Awake()
    {
        uiManager = FindFirstObjectByType<UIManager>();
        ghostManager = FindFirstObjectByType<GhostManager>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }
    public void PlayIntro() { Play(bgmIntro, false); }
    public void PlayScaredMusic() { RefreshGhostMusic(); }
    public void PlayNormalMusic() { RefreshGhostMusic(); }
    public void PlayDeadMusic() { RefreshGhostMusic(); }
    public void RefreshGhostMusic()
    {
        if (uiManager == null || !uiManager.IsRoundRunning) return;
        Play(ghostManager != null && ghostManager.HasDeadGhost ? bgmGhostDead
            : uiManager.GetScaredTimeRemaining() > 0f ? bgmGhostScared : bgmGhostNormal, true);
    }
    private void Play(AudioClip clip, bool loop)
    {
        if (audioSource == null || clip == null) return;
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        audioSource.clip = clip; audioSource.loop = loop; audioSource.Play();
    }
}
