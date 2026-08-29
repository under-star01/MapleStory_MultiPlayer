using UnityEngine;

public class ActionStateBehaviour : StateMachineBehaviour
{
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        PlayerAnimController playerAnim =
            animator.GetComponent<PlayerAnimController>();

        playerAnim?.OnActionStateExited();
    }
}