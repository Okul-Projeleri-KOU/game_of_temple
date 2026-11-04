using System.Collections;
using UnityEngine;

namespace Cainos.PixelArtTopDown_Basic
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float lerpSpeed = 1.0f;

        private Vector3 offset;
        private Vector3 targetPos;
        private Transform originalTarget;
        private Coroutine temporaryFocusCoroutine;

        private void Start()
        {
            if (target == null)
                return;

            originalTarget = target;
            offset = transform.position - target.position;
        }

        private void Update()
        {
            if (target == null)
                return;

            targetPos = target.position + offset;
            float currentSpeed = target != originalTarget ? Mathf.Max(lerpSpeed * 2.2f, 4.5f) : lerpSpeed;
            transform.position = Vector3.Lerp(transform.position, targetPos, currentSpeed * Time.deltaTime);
        }

        public void FocusTemporarily(Transform focusTarget, float duration = 1.6f)
        {
            if (focusTarget == null || !gameObject.activeInHierarchy)
                return;

            if (originalTarget == null)
                originalTarget = target;

            if (temporaryFocusCoroutine != null)
            {
                StopCoroutine(temporaryFocusCoroutine);
            }

            temporaryFocusCoroutine = StartCoroutine(TemporaryFocusRoutine(focusTarget, duration));
        }

        private IEnumerator TemporaryFocusRoutine(Transform focusTarget, float duration)
        {
            target = focusTarget;
            yield return new WaitForSeconds(duration);
            if (originalTarget != null)
            {
                target = originalTarget;
            }
            temporaryFocusCoroutine = null;
        }
    }
}
