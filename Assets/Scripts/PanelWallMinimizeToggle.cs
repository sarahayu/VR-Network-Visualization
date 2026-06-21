using System.Collections;
using UnityEngine;

public class PanelWallMinimizeToggle : MonoBehaviour
{
    [SerializeField] private Transform wallAnchor;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private Vector3 minimizedScale = new Vector3(0.35f, 0.35f, 0.35f);

    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private bool isOnWall;
    private Coroutine moveRoutine;

    void Start()
    {
        originalParent = transform.parent;
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
    }

    public void Toggle()
    {
        if (wallAnchor == null)
            return;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        if (isOnWall)
        {
            transform.SetParent(originalParent, true);
            moveRoutine = StartCoroutine(MoveTo(
                originalPosition,
                originalRotation,
                originalScale
            ));
        }
        else
        {
            transform.SetParent(null, true);
            moveRoutine = StartCoroutine(MoveTo(
                wallAnchor.position,
                wallAnchor.rotation,
                minimizedScale
            ));
        }

        isOnWall = !isOnWall;
    }

    private IEnumerator MoveTo(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.SetPositionAndRotation(targetPosition, targetRotation);
        transform.localScale = targetScale;
        moveRoutine = null;
    }
}