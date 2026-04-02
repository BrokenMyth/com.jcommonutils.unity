using UnityEngine;

namespace GorillaLocomotion.Extensions
{
    public static class GameObjectExtension
    {
        public static void PlayVfx(this GameObject o)
        {
            var componentsInChildren = o.GetComponentsInChildren<ParticleSystem>();
            foreach (var componentsInChild in componentsInChildren)
            {
                componentsInChild.Play();
            }
        }
    }
}