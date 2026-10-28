using UnityEngine;

public class RaycastSensorController : MonoBehaviour
{
    [Header("Sensör Ayarları")]
    public float sensorRange = 5f; 
    public LayerMask obstacleLayer; 

    
    public bool IsObstacleAhead()
    {
        RaycastHit hit;
        
        if (Physics.Raycast(transform.position, transform.forward, out hit, sensorRange, obstacleLayer))
        {
            Debug.Log($"Sensör Uyarısı: {hit.distance:F1} metre ileride '{hit.collider.name}' tespit edildi!");
            return true;
        }
        return false;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * sensorRange);
    }

   
    
}