using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ModernButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Animation Settings")]
    public float hoverScale = 0.97f;      // Slight shrink on hover
    public float pressedScale = 0.93f;    // More shrink on click
    public float animationDuration = 0.1f; // How fast it animates

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private bool isPressed = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isPressed)
            AnimateToScale(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateToScale(isPressed ? pressedScale : 1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        AnimateToScale(pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        AnimateToScale(1f);
    }

    private void AnimateToScale(float targetScaleFactor)
    {
        StopAllCoroutines();
        StartCoroutine(ScaleCoroutine(targetScaleFactor));
    }

    System.Collections.IEnumerator ScaleCoroutine(float targetScaleFactor)
    {
        Vector3 startScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * targetScaleFactor;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / animationDuration);
            elapsed += Time.unscaledDeltaTime; // Works even in pause menus
            yield return null;
        }

        rectTransform.localScale = targetScale;
    }
}