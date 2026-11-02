using UnityEngine;
using UnityEngine.AI;

public class NavMeshTest : MonoBehaviour
{
    public Transform hedefKuresi;
    private NavMeshAgent ajanMotoru;

    private void Start()
    {
        CompanionAgent companion = GetComponent<CompanionAgent>();
        if (companion != null && companion.enabled)
        {
            return;
        }

        ajanMotoru = GetComponent<NavMeshAgent>();

        if (ajanMotoru != null && hedefKuresi != null && ajanMotoru.isOnNavMesh)
        {
            ajanMotoru.SetDestination(hedefKuresi.position);
        }
    }
}
