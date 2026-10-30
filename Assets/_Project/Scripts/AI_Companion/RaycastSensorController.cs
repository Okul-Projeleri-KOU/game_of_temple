using UnityEngine;

public class RaycastSensorController : MonoBehaviour
{
    [Header("Sensör Ayarları")]
    public float isinUzunlugu = 3f;
    public float yanIsinAcisi = 35f; 

    public bool IsObstacleAhead()
    {
        Vector3 merkez = transform.position;
        Vector3 ileri = transform.forward;
        Vector3 sagCapraz = Quaternion.Euler(0, yanIsinAcisi, 0) * ileri;
        Vector3 solCapraz = Quaternion.Euler(0, -yanIsinAcisi, 0) * ileri;

        
        bool onDolu = Physics.Raycast(merkez, ileri, isinUzunlugu);
        bool sagDolu = Physics.Raycast(merkez, sagCapraz, isinUzunlugu);
        bool solDolu = Physics.Raycast(merkez, solCapraz, isinUzunlugu);

        return onDolu || sagDolu || solDolu;
    }

    
    private void OnDrawGizmos()
    {
        Vector3 merkez = transform.position;
        Vector3 ileri = transform.forward;
        Vector3 sagCapraz = Quaternion.Euler(0, yanIsinAcisi, 0) * ileri;
        Vector3 solCapraz = Quaternion.Euler(0, -yanIsinAcisi, 0) * ileri;

        Gizmos.color = Color.red;

        
        Gizmos.DrawRay(merkez, ileri * isinUzunlugu);
        Gizmos.DrawRay(merkez, sagCapraz * isinUzunlugu);
        Gizmos.DrawRay(merkez, solCapraz * isinUzunlugu);
    }
}