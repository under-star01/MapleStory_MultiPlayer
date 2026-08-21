using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SkillPaletteIconUI :
    MonoBehaviour,
    IPointerClickHandler
{
    [Header("Skill")]
    [SerializeField]
    private SkillId skillId;

    [Header("Reference")]
    [SerializeField]
    private QuickSlotSettingUI quickSlotSettingUI;

    public void OnPointerClick(
        PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        if (quickSlotSettingUI == null)
        {
            Debug.LogWarning(
                $"{name}에 {nameof(QuickSlotSettingUI)}가 " +
                "연결되지 않았습니다.",
                this
            );

            return;
        }

        if (skillId == SkillId.None)
            return;

        quickSlotSettingUI.PickSkill(skillId);
    }
}