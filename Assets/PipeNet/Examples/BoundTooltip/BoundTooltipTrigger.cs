using UnityEngine;
using UnityEngine.EventSystems;

public class BoundTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [TextAreaAttribute]
    public string text;

    public bool useMousePosition = false;

    public Vector3 offset;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (useMousePosition)
        {
            StartHover(new Vector3(eventData.position.x, eventData.position.y, 0f));
        }
        else
        {
            StartHover(transform.position + offset);
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        StartHover(transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        StopHover();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        StopHover();
    }

    void StartHover(Vector3 position)
    {
        BoundTooltipItem.Instance.ShowTooltip(text, position);
    }

    void StopHover()
    {
        BoundTooltipItem.Instance.HideTooltip();
    }
}

