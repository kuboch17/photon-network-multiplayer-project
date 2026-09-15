using System.Collections;
using UnityEngine;

namespace RPGCharacterAnims
{
    //Placeholder functions for Animation events.
    public class RPGCharacterAnimatorEvents:MonoBehaviour
    {
		[HideInInspector] public RPGCharacterController rpgCharacterController;

		public void Hit()
        {
        }

        public void Shoot()
        {
        }

        public void FootR()
        {
        }

        public void FootL()
        {
        }

        public void Land()
        {
        }

		public void WeaponSwitch()
		{
			if(rpgCharacterController.rpgCharacterWeaponController != null)
			{
				rpgCharacterController.rpgCharacterWeaponController.WeaponSwitch();
			}
		}
	}
}