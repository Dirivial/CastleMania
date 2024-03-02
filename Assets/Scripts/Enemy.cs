using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private int id;
    public int damage;
    public float speed;
    public float health;
    public Action<int, Enemy, bool> Despawn;
    public MMFeedbacks dieFeedback;
    public MMFeedbacks aliveFeedback;

    private MMHealthBar healthBar;

    private float currentHealth;

    private void Awake()
    {
        healthBar = GetComponent<MMHealthBar>();
    }

    public void Create(Action<int, Enemy, bool> despawnAction)
    {
        Despawn = despawnAction;
        aliveFeedback.PlayFeedbacks();
        healthBar.UpdateBar(currentHealth, 0, health, true);
    }

    public void Spawn()
    {
        currentHealth = health;
        aliveFeedback.PlayFeedbacks();
        healthBar.UpdateBar(currentHealth, 0, health, true);
    }

    public void GotHit(float damage)
    {
        this.currentHealth -= damage;
        
        if (this.currentHealth <= 0)
        {
            //Debug.Log("Ded");
            aliveFeedback.StopFeedbacks();
            healthBar.UpdateBar(currentHealth, 0, health, true);
            aliveFeedback.transform.position = new Vector3(500, 500, 500);
            dieFeedback.PlayFeedbacks(transform.position);
            Despawn(id, this, true);
        } else
        {
            healthBar.UpdateBar(currentHealth, 0, health, true);
        }
    }

    public void Remove(bool gotKilled)
    {
        aliveFeedback.StopFeedbacks();
        healthBar.UpdateBar(0, 0, health, true);
        aliveFeedback.transform.position = new Vector3(500, 500, 500);
        dieFeedback.PlayFeedbacks(transform.position);
        Despawn(id, this, gotKilled);
    }

    public void SetID(int id)
    {
        this.id = id;
    }
}
