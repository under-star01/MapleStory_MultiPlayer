using UnityEngine;

public class MonsterHitStateBehaviour :
    StateMachineBehaviour
{
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        MonsterHealth monsterHealth =
            animator.GetComponent<MonsterHealth>();

        if (monsterHealth == null)
            return;

        monsterHealth.OnHitAnimationEnded();
    }
}