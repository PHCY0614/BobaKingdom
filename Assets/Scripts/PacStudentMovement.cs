using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PacStudentMover : MonoBehaviour
{
    public Vector3[] waypoints = new Vector3[]
    {
        new Vector3(1, 14, 0),  // top right
        new Vector3(-4, 14, 0), // top left
        new Vector3(-4, 10, 0), // bottom left
        new Vector3(1, 10, 0)   // bottom right
    };

    public float moveDuration = 2f;

    private int currentIndex = 0;
    private float elapsedTime = 0f;
    private AudioSource audioSource;
    private Vector3 lastPosition;
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 start = waypoints[currentIndex];
        Vector3 end = waypoints[(currentIndex + 1) % waypoints.Length];

        elapsedTime += Time.deltaTime;
        float t = elapsedTime / moveDuration;

        transform.position = Vector3.Lerp(start, end, t);
        
        //Check moving 
        if (Vector3.Distance(transform.position, lastPosition) > 0.01f)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
        }
        
        lastPosition = transform.position;
        
        //next waypoint
        if (t >= 1f)
        {
            elapsedTime = 0f;
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
    }
}