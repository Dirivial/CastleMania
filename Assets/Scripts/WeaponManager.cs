

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

public class WeaponManager : MonoBehaviour
{
    [SerializeField] private List<AbstractWeapon> weapons;
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private MMFeedbacks MMFeedbacks;
    [SerializeField] private MMFeedbacks BulletHitFeedback;

    private MMF_ParticlesInstantiation particlesInstantiation;

    private int currentWeapon = 0;
    ObjectPool<Bullet> bulletPool;

    public void Awake()
    {
        bulletPool = new ObjectPool<Bullet>(() =>
        {
            Bullet b = Instantiate(bulletPrefab);
            b.Init(KillBullet);
            return b;
        }, bullet => {
            bullet.gameObject.SetActive(true);
        }, bullet =>
        {
            bullet.gameObject.SetActive(false);
        }, obj =>
        {
            Destroy(obj);
        }, false, 100, 1000);
    }

    public void Start()
    {
        //particlesInstantiation = BulletHitFeedback.GetComponent<MMF_ParticlesInstantiation>();
    }



    public void OnScroll(bool next)
    {
        if (weapons.Count <= 1) return;
        currentWeapon = next ? (currentWeapon + 1) % weapons.Count : (currentWeapon - 1) % weapons.Count;
    }

    public void OnShoot(Vector3 position, Vector3 forward, Quaternion rotation)
    {
        Bullet bullet = bulletPool.Get();
        bullet.transform.position = position;
        bullet.transform.rotation = rotation;
        bullet.SetForward(forward);
        bullet.ResetTimer();

        MMFeedbacks.PlayFeedbacks(position + forward * 0.5f);
    }

    private void KillBullet(Bullet bullet)
    {
        //particlesInstantiation.InstantiateParticlesPosition = bullet.transform;
        BulletHitFeedback.PlayFeedbacks(bullet.transform.position);
        bulletPool.Release(bullet);
    }
}