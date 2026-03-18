using Knife.Effects.SimpleController;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Knife.ScifiEffects
{
    [RequireComponent(typeof(Animator))]
    public class GravityGunDistortion : MonoBehaviour
    {
        [SerializeField] private GravityGunWeapon gravityGunWeapon;

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();

            if (gravityGunWeapon == null)
                gravityGunWeapon = GetComponentInParent<GravityGunWeapon>();
        }

        private void OnEnable()
        {
            if (gravityGunWeapon == null)
                return;

            gravityGunWeapon.OnGrab -= OnGrab;
            gravityGunWeapon.OnLeave -= OnLeave;
            gravityGunWeapon.OnThrow -= OnThrow;

            gravityGunWeapon.OnGrab += OnGrab;
            gravityGunWeapon.OnLeave += OnLeave;
            gravityGunWeapon.OnThrow += OnThrow;
        }

        private void OnDisable()
        {
            if (gravityGunWeapon == null)
                return;

            gravityGunWeapon.OnGrab -= OnGrab;
            gravityGunWeapon.OnLeave -= OnLeave;
            gravityGunWeapon.OnThrow -= OnThrow;
        }

        private void OnThrow()
        {
            if (animator == null)
                return;
            animator.Play("Gravity Gun Distortion OUT", 0, 0);
        }

        private void OnLeave()
        {
            if (animator == null)
                return;
            animator.Play("Gravity Gun Distortion OUT ZERO", 0, 0);
        }

        private void OnGrab()
        {
            if (animator == null)
                return;
            animator.Play("Gravity Gun Distortion IN", 0, 0);
        }
    }
}
