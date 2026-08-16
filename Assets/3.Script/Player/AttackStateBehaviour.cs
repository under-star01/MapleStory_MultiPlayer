using UnityEngine;

public class AttackStateBehaviour : StateMachineBehaviour
{
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        animator.GetComponent<PlayerMove>().EndAttack();
    }
}