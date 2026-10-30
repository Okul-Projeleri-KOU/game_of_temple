using UnityEngine;
using UnityEngine.AI; 

public class NavMeshTest : MonoBehaviour
{
    public Transform hedefKuresi;
    private NavMeshAgent ajanMotoru;

    void Start()
    {
        ajanMotoru = GetComponent<NavMeshAgent>();
        
        if(hedefKuresi != null)
        {
            
            ajanMotoru.SetDestination(hedefKuresi.position);
        }
    }
}