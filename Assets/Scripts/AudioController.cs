using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip bgmIntro;
    public AudioClip bgmGhostNormal;
    public AudioClip bgmGhostScared;
    public AudioClip bgmGhostDead;
    private Coroutine deadMusicCoroutine; // 追蹤當前的 Dead Music 協程
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(PlayBGM());
    }

    IEnumerator PlayBGM()
    {
        //1. Play Intro Music
        audioSource.clip = bgmIntro;
        audioSource.Play();
        
        //2. Wait for 3 Seconds
        yield return new WaitForSeconds(3.0f);
        
        //3. Loop BGM
        audioSource.clip = bgmGhostNormal;
        audioSource.loop = true;
        audioSource.Play();
    }
    
    public void PlayScaredMusic()
    {
        if (audioSource != null && bgmGhostScared != null)
        {
            // 🔧 如果正在播放 Dead Music，取消協程
            if (deadMusicCoroutine != null)
            {
                StopCoroutine(deadMusicCoroutine);
                deadMusicCoroutine = null;
            }
            if (audioSource.clip != bgmGhostScared)
            {
                audioSource.clip = bgmGhostScared;
                audioSource.loop = true;
                audioSource.Play();
                Debug.Log("AudioController: Switched to Scared BGM.");
            }
        }
    }
    
    public void PlayNormalMusic()
    {
        if (audioSource != null && audioSource.clip != bgmGhostNormal)
        {
            // 🔧 如果正在播放 Dead Music，取消協程
            if (deadMusicCoroutine != null)
            {
                StopCoroutine(deadMusicCoroutine);
                deadMusicCoroutine = null;
            }
            
            audioSource.clip = bgmGhostNormal;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log("AudioController: Switched back to Normal BGM.");
        }
    }
    public void PlayDeadMusic()
    {
        if (audioSource != null && bgmGhostDead != null)
        {
            // 停止當前音樂
            audioSource.Stop();
            
            // 播放死亡音樂 (通常是單次播放)
            audioSource.clip = bgmGhostDead;
            audioSource.loop = false; // Dead BGM 不循環播放
            audioSource.Play();
            Debug.Log("AudioController: Switched to Dead BGM.");
            
            if (deadMusicCoroutine != null)
            {
                StopCoroutine(deadMusicCoroutine);
            }
            deadMusicCoroutine = StartCoroutine(WaitAndSwitchBackBGM());
        }
    }

    // ✨ 新增協程：等待後根據 GhostManager 的狀態切回
    private IEnumerator WaitAndSwitchBackBGM()
    {
        // 等待 Dead BGM 播放完畢（或 3 秒，取較短者）
        float waitTime = bgmGhostDead != null ? Mathf.Min(bgmGhostDead.length, 3.0f) : 3.0f;
        yield return new WaitForSecondsRealtime(waitTime);
        
        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            float scaredTime = uiManager.GetScaredTimeRemaining();
            
            // 🔧 更精確的狀態判斷
            if (scaredTime > 3.0f)
            {
                // 還有超過 3 秒 → Scared
                PlayScaredMusic();
            }
            else if (scaredTime > 0f)
            {
                // 0~3 秒 → Recovering (仍使用 Scared 音樂)
                PlayScaredMusic();
            }
            else
            {
                // 時間結束 → Normal
                PlayNormalMusic();
            }
        }
        else
        {
            PlayNormalMusic();
        }
        
        deadMusicCoroutine = null;
        }
    }
