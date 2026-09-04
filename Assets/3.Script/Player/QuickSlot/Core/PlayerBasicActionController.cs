using System.Collections.Generic;
using UnityEngine;

public class PlayerBasicActionController : MonoBehaviour
{
    private readonly Dictionary<BasicActionId, BasicActionData>
        actions = new();

    public bool TryGetAction(
        BasicActionId actionId,
        out BasicActionData actionData)
    {
        return actions.TryGetValue(
            actionId,
            out actionData
        );
    }

    public bool Execute(
        BasicActionId actionId)
    {
        if (!actions.TryGetValue(
                actionId,
                out BasicActionData actionData) ||
            actionData.Command == null)
        {
            return false;
        }

        return actionData.Command.Execute();
    }

    public bool Register(
        BasicActionData actionData)
    {
        if (actionData == null ||
            actionData.ActionId == BasicActionId.None ||
            actionData.Icon == null ||
            actionData.Command == null)
        {
            return false;
        }

        actions[actionData.ActionId] =
            actionData;

        return true;
    }

    public void Clear()
    {
        actions.Clear();
    }
}