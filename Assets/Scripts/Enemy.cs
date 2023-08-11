using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private int id;
    public int damage;
    public float speed;
    public Quaternion rotation;
    public Vector3 position;
    public float health;
    public Action<int, Enemy> Despawn;

    private float currentHealth;

    public void Create(Action<int, Enemy> despawnAction)
    {
        Despawn = despawnAction;
    }

    public void Spawn()
    {
        currentHealth = health;
    }

    public void GotHit(float damage)
    {
        this.currentHealth -= damage;
        if (this.currentHealth <= 0)
        {
            //Debug.Log("Ded");
            Despawn(id, this);
        }
    }

    public void SetID(int id)
    {
        this.id = id;
    }
}
