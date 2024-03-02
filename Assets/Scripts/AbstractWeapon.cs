using UnityEngine;

public abstract class AbstractWeapon
{
    private string name;
    private GameObject weaponObject;
    private float damage;
    private float shootTimeout;
    private float wieldTimeout;
    private float projectileSpeed;
    private GameObject projectileObject;

    protected float Damage { get => damage; set => damage = value; }
    protected float ShootTimeout { get => shootTimeout; set => shootTimeout = value; }
    protected float WieldTimeout { get => wieldTimeout; set => wieldTimeout = value; }
    protected float ProjectileSpeed { get => projectileSpeed; set => projectileSpeed = value; }
    protected GameObject WeaponObject { get => weaponObject; set => weaponObject = value; }
    protected string Name { get => name; set => name = value; }
    protected GameObject ProjectileObject { get => projectileObject; set => projectileObject = value; }

    public abstract void Shoot(Vector3Int origin, Quaternion direction);
}