using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip bgmIntro;
    public AudioClip bgmGhostNormal;
    public AudioClip bgmGhostScared;
    
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
    
    public void PlayScaredMusic(AudioClip scaredClip)
    {
        if (audioSource != null && scaredClip != null)
        {
            // 覆蓋當前播放的音樂
            audioSource.clip = scaredClip;
            audioSource.loop = true; 
            audioSource.Play();
            Debug.Log("AudioController: Switched to Scared BGM.");
        }
    }
    
    public void PlayNormalMusic()
    {
        if (audioSource != null && audioSource.clip != bgmGhostNormal)
        {
            // 切換回 Normal BGM
            audioSource.clip = bgmGhostNormal;
            audioSource.loop = true;
            audioSource.Play();
            Debug.Log("AudioController: Switched back to Normal BGM.");
        }
    }
    public void PlayScaredMusic()
    {
        if (audioSource != null && bgmGhostScared != null)
        {
            // 檢查是否已經在播放這個音樂，避免重複切換
            if (audioSource.clip != bgmGhostScared)
            {
                // 覆蓋當前播放的音樂
                audioSource.clip = bgmGhostScared;
                audioSource.loop = true; // BGM 通常是循環的
                audioSource.Play();
                Debug.Log("AudioController: Switched to Scared BGM.");
            }
        }
    }
    
}
