using UnityEngine;

public class Weapon : AbstractWeapon
{
    //[SerializeField] private float fireRate = 0.0f;

    public override void Shoot(Vector3Int origin, Quaternion direction)
    {
        Debug.Log("Trying to shoot from " + origin);
    }
}