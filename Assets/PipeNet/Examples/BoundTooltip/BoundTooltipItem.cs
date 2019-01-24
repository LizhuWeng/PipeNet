using UnityEngine;
using UnityEngine.UI;

public class BoundTooltipItem : MonoBehaviour
{
    public bool IsActive
    {
        get
        {
            return gameObject.activeSelf;
        }
    }

    public Text TooltipText;
    public Vector3 ToolTipOffset;

    void Awake()
    {
        instance = this;
        if (!TooltipText) TooltipText = GetComponentInChildren<Text>();
        HideTooltip();
    }

    public void ShowTooltip(string text, Vector3 pos)
    {
        if (TooltipText.text != text)
            TooltipText.text = text;

        transform.position = pos + ToolTipOffset;

        gameObject.SetActive(true);
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
    }

    // Standard Singleton Access
    private static BoundTooltipItem instance;
    public static BoundTooltipItem Instance
    {
        get
        {
            if (instance == null)
                instance = GameObject.FindObjectOfType<BoundTooltipItem>();
            return instance;
        }
    }
}



