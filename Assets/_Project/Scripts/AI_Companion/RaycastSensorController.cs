using UnityEngine;
using UnityEngine.AI;

public class RaycastSensorController : MonoBehaviour
{
    [Header("Sensör Ayarları")]
    public float isinUzunlugu = 3f;
    public float yanIsinAcisi = 35f;
    public LayerMask engelKatmani = ~0;

    private NavMeshAgent ajanMotoru;
    private Vector2 sonHareketYonu2D = Vector2.up;

    private void Awake()
    {
        ajanMotoru = GetComponent<NavMeshAgent>();
    }

    public bool IsObstacleAhead()
    {
        if (Is2DMode())
        {
            Get2DRayDirections(out Vector2 merkez2D, out Vector2 ileri2D, out Vector2 sagCapraz2D, out Vector2 solCapraz2D);

            bool onDolu2D = Cast2DObstacleRay(merkez2D, ileri2D);
            bool sagDolu2D = Cast2DObstacleRay(merkez2D, sagCapraz2D);
            bool solDolu2D = Cast2DObstacleRay(merkez2D, solCapraz2D);

            return onDolu2D || sagDolu2D || solDolu2D;
        }

        Vector3 merkez = transform.position;
        Vector3 ileri = Get3DForwardDirection();
        Vector3 sagCapraz = Quaternion.Euler(0f, yanIsinAcisi, 0f) * ileri;
        Vector3 solCapraz = Quaternion.Euler(0f, -yanIsinAcisi, 0f) * ileri;

        bool onDolu = Physics.Raycast(merkez, ileri, isinUzunlugu, engelKatmani, QueryTriggerInteraction.Ignore);
        bool sagDolu = Physics.Raycast(merkez, sagCapraz, isinUzunlugu, engelKatmani, QueryTriggerInteraction.Ignore);
        bool solDolu = Physics.Raycast(merkez, solCapraz, isinUzunlugu, engelKatmani, QueryTriggerInteraction.Ignore);

        return onDolu || sagDolu || solDolu;
    }

    private bool Cast2DObstacleRay(Vector2 origin, Vector2 direction)
    {
        RaycastHit2D singleHit = Physics2D.Raycast(origin, direction, isinUzunlugu, engelKatmani);
        if (singleHit.collider != null && !singleHit.collider.isTrigger && singleHit.collider.gameObject != gameObject)
        {
            return true;
        }

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, isinUzunlugu, engelKatmani);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.gameObject == gameObject)
                continue;

            return true;
        }

        return false;
    }

    private bool Is2DMode()
    {
        if (ajanMotoru == null)
        {
            ajanMotoru = GetComponent<NavMeshAgent>();
        }

        if (ajanMotoru != null && !ajanMotoru.updateUpAxis)
        {
            return true;
        }

        return Object.FindFirstObjectByType<Grid>() != null;
    }

    private void Get2DRayDirections(out Vector2 merkez, out Vector2 ileri, out Vector2 sagCapraz, out Vector2 solCapraz)
    {
        merkez = transform.position;

        if (ajanMotoru != null)
        {
            Vector2 vel2D = new Vector2(ajanMotoru.velocity.x, ajanMotoru.velocity.y);
            if (vel2D.sqrMagnitude > 0.01f)
            {
                sonHareketYonu2D = vel2D.normalized;
            }
            else
            {
                Vector2 desired2D = new Vector2(ajanMotoru.desiredVelocity.x, ajanMotoru.desiredVelocity.y);
                if (desired2D.sqrMagnitude > 0.01f)
                {
                    sonHareketYonu2D = desired2D.normalized;
                }
            }
        }

        ileri = sonHareketYonu2D.sqrMagnitude > 0.001f ? sonHareketYonu2D.normalized : Vector2.up;
        sagCapraz = (Vector2)(Quaternion.Euler(0f, 0f, -yanIsinAcisi) * ileri);
        solCapraz = (Vector2)(Quaternion.Euler(0f, 0f, yanIsinAcisi) * ileri);
    }

    private Vector3 Get3DForwardDirection()
    {
        if (ajanMotoru != null && ajanMotoru.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 flatVel = new Vector3(ajanMotoru.velocity.x, 0f, ajanMotoru.velocity.z);
            if (flatVel.sqrMagnitude > 0.01f)
            {
                return flatVel.normalized;
            }
        }
        return transform.forward;
    }

    private void OnDrawGizmos()
    {
        if (Is2DMode())
        {
            Get2DRayDirections(out Vector2 merkez2D, out Vector2 ileri2D, out Vector2 sagCapraz2D, out Vector2 solCapraz2D);
            Vector3 merkez3D = new Vector3(merkez2D.x, merkez2D.y, transform.position.z);

            Gizmos.color = Cast2DObstacleRay(merkez2D, ileri2D) ? Color.yellow : Color.red;
            Gizmos.DrawRay(merkez3D, (Vector3)(ileri2D * isinUzunlugu));

            Gizmos.color = Cast2DObstacleRay(merkez2D, sagCapraz2D) ? Color.yellow : Color.red;
            Gizmos.DrawRay(merkez3D, (Vector3)(sagCapraz2D * isinUzunlugu));

            Gizmos.color = Cast2DObstacleRay(merkez2D, solCapraz2D) ? Color.yellow : Color.red;
            Gizmos.DrawRay(merkez3D, (Vector3)(solCapraz2D * isinUzunlugu));
            return;
        }

        Vector3 merkez = transform.position;
        Vector3 ileri = Get3DForwardDirection();
        Vector3 sagCapraz = Quaternion.Euler(0f, yanIsinAcisi, 0f) * ileri;
        Vector3 solCapraz = Quaternion.Euler(0f, -yanIsinAcisi, 0f) * ileri;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(merkez, ileri * isinUzunlugu);
        Gizmos.DrawRay(merkez, sagCapraz * isinUzunlugu);
        Gizmos.DrawRay(merkez, solCapraz * isinUzunlugu);
    }
}
