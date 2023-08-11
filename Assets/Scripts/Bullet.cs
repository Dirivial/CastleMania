using System;
using System.Collections;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    private Action<Bullet> _killAction;
    public float Velocity = 1.0f;
    public float LifeTime = 3.0f;
    public float Damage = 10f;
    private Rigidbody rb;
    private Vector3 forward;

    public void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Init(Action<Bullet> killAction)
    {
        _killAction = killAction;
        StartCoroutine(DestroyAfterTime());
    }

    public void SetForward(Vector3 forward)
    {
        this.forward = forward;
    }

    public void ResetTimer()
    {
        StartCoroutine(DestroyAfterTime());
    }

    private IEnumerator DestroyAfterTime()
    {
        yield return new WaitForSeconds(LifeTime);

        _killAction(this);
    }

    public void Update()
    {
        rb.velocity = forward * Velocity;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.transform.CompareTag("Environment")) { _killAction(this); }
        if (other.transform.CompareTag("Enemy"))
        {
            other.SendMessage("GotHit", Damage);
            _killAction(this);
        }
    }
}