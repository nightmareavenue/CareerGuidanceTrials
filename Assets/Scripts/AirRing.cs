using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class AirRing : MonoBehaviour
{
    void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var score = other.GetComponentInParent<AirRingScore>();
        if (score == null) return;

        score.AddPoint();
        gameObject.SetActive(false);
    }
}