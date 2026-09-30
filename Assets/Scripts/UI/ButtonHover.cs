using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 originalScale;

    void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        transform.localScale = originalScale * 1.05f;
    }

    public void OnPointerExit(PointerEventData e)
    {
        transform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData e)
    {
        transform.localScale = originalScale * 0.95f;
    }

    public void OnPointerUp(PointerEventData e)
    {
        transform.localScale = originalScale * 1.05f;
    }
}