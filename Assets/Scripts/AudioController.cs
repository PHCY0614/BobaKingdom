using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    private UIManager uiManager;
    public AudioSource audioSource;
    public AudioClip bgmIntro;
    public AudioClip bgmGhostNormal;
    public AudioClip bgmGhostScared;
    public AudioClip bgmGhostDead;
    private Coroutine deadMusicCoroutine; 
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
            audioSource.Stop();
            
            audioSource.clip = bgmGhostDead;
            audioSource.loop = false; 
            audioSource.Play();
            Debug.Log("AudioController: Switched to Dead BGM.");
            
            if (deadMusicCoroutine != null)
            {
                StopCoroutine(deadMusicCoroutine);
            }
            deadMusicCoroutine = StartCoroutine(WaitAndSwitchBackBGM());
        }
    }
    
    private IEnumerator WaitAndSwitchBackBGM()
    {
        // Wait for Dead BGM finish or wait for 3 secs. 
        float waitTime = bgmGhostDead != null ? Mathf.Min(bgmGhostDead.length, 3.0f) : 3.0f;
        yield return new WaitForSecondsRealtime(waitTime);
        
        uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            float scaredTime = uiManager.GetScaredTimeRemaining();
            
            if (scaredTime > 3.0f)
            {
                PlayScaredMusic();
            }
            else if (scaredTime > 0f)
            {
                PlayScaredMusic();
            }
            else
            {
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
