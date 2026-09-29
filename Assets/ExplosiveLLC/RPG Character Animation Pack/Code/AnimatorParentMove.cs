using UnityEngine;

namespace RPGCharacterAnims
{
	public class AnimatorParentMove:MonoBehaviour
	{
		[HideInInspector] public Animator anim;
		[HideInInspector] public RPGCharacterMovementController rpgCharacterMovementController;

		void OnAnimatorMove()
		{
			if(!rpgCharacterMovementController.canMove)
			{
				transform.parent.rotation = anim.rootRotation;
				transform.parent.position += anim.deltaPosition;
			}
		}
	}
}
