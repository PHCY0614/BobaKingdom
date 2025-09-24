using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip bgmIntro;
    public AudioClip bgmGhostNormal;
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

    // Update is called once per frame
    void Update()
    {
        
    }
}
