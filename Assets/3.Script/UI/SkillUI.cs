using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class SkillUI : MonoBehaviour
{
    [SerializeField]
    private bool startOpened;

    private CanvasGroup canvasGroup;

    public bool IsOpened { get; private set; }

    private void Awake()
    {
        canvasGroup =
            GetComponent<CanvasGroup>();

        SetWindowVisible(startOpened);
    }

    public void Open()
    {
        SetWindowVisible(true);
    }

    public void Close()
    {
        SetWindowVisible(false);
    }

    public void Toggle()
    {
        SetWindowVisible(
            !IsOpened
        );
    }

    private void SetWindowVisible(
        bool visible)
    {
        IsOpened = visible;

        canvasGroup.alpha =
            visible ? 1f : 0f;

        canvasGroup.interactable =
            visible;

        canvasGroup.blocksRaycasts =
            visible;
    }
}