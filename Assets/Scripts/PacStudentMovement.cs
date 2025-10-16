using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PacStudentMover : MonoBehaviour
{
    public Vector3[] waypoints = new Vector3[]
    {
        new Vector3(1, 14, 0), // top right
        new Vector3(-4, 14, 0), // top left
        new Vector3(-4, 10, 0), // bottom left
        new Vector3(1, 10, 0) // bottom right
    };

    public float moveSpeed = 2f;

    private int currentIndex = 0;
    private AudioSource audioSource;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.loop = true;
            audioSource.Play(); 
        }
    }

    void Update()
    {
        Vector3 targetWaypoint = waypoints[currentIndex];

        if (Vector3.Distance(transform.position, targetWaypoint) < 0.01f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            targetWaypoint = waypoints[currentIndex];
        }

        Vector3 movementDirection = (targetWaypoint - transform.position).normalized;

        transform.position += movementDirection * moveSpeed * Time.deltaTime;
        
        // animation
        if (animator != null)
        {
            animator.SetFloat("Speed", movementDirection.magnitude > 0 ? 1f : 0f);

            if (Mathf.Abs(movementDirection.x) > Mathf.Abs(movementDirection.y))
            {
                // horizontal
                if (movementDirection.x > 0)
                {
                    animator.SetInteger("Direction", 3); // right
                }
                else
                {
                    animator.SetInteger("Direction", 1); // left
                }
            }
            else
            {
                // vertical
                if (movementDirection.y > 0)
                {
                    animator.SetInteger("Direction", 0); // up
                }
                else
                {
                    animator.SetInteger("Direction", 2); // down
                }
            }
        }
    }
}    