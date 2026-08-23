using System;
using UnityEngine;

public class LocalPlayerContext
{
    public GameObject Player { get; }
    public PlayerMove Move { get; }
    public PlayerSkillController Skill { get; }
    public PlayerQuickSlotController QuickSlot { get; }

    public LocalPlayerContext(GameObject player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(
                nameof(player)
            );
        }

        Player = player;

        Move =
            player.GetComponent<PlayerMove>();

        Skill =
            player.GetComponent<PlayerSkillController>();

        QuickSlot =
            player.GetComponent<PlayerQuickSlotController>();

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (Move == null)
        {
            Debug.LogError(
                $"{nameof(PlayerMove)}를 찾지 못했습니다.",
                Player
            );
        }

        if (Skill == null)
        {
            Debug.LogError(
                $"{nameof(PlayerSkillController)}를 찾지 못했습니다.",
                Player
            );
        }

        if (QuickSlot == null)
        {
            Debug.LogError(
                $"{nameof(PlayerQuickSlotController)}를 찾지 못했습니다.",
                Player
            );
        }
    }
}