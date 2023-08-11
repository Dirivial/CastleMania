using MoreMountains.Feedbacks;
using System;
using UnityEngine;

public class GrapplingHook : MonoBehaviour
{
    public float MaxLength = 100f;
    public LayerMask HookableLayer;
    public GameObject Hook;
    public GameObject PointPrefab;

    public MMFeedbacks HookFoundTargetFeedback;
    public MMFeedbacks HookMissedTargetFeedback;
    public MMFeedbacks HookCoolingDownFeedback;

    public bool ShootHook(Action<Vector3> targetFound, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(Hook.transform.position, direction, out hit, MaxLength, HookableLayer))
        {

            if (hit.collider.CompareTag("Environment"))
            {
                targetFound(hit.point);
                HookFoundTargetFeedback.PlayFeedbacks(hit.point);
                PointPrefab.transform.position = hit.point;
                PointPrefab.SetActive(true);
                return true;
            }
        }

        HookMissedTargetFeedback.PlayFeedbacks();
        return false;
    }

    public void UnHook()
    {
        PointPrefab.SetActive(false);
        HookCoolingDownFeedback.PlayFeedbacks();
    }
}
