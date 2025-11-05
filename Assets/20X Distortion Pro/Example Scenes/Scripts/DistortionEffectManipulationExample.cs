using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace DistortionsPro_20X
{
    public class DistortionEffectManipulationExample : MonoBehaviour
    {
        // Post-processing volume with FlowMosh effect;
        public Volume volume;

        // Temp FlowMosh effect.
        private FlowMosh m_Effect;

        private void Start()
        {
            //Null check
            if (volume == null)
                return;

            // Get refference to Distortion effect
            volume.profile.TryGet(out m_Effect);

            //Null check
            if (m_Effect is null)
            {
                Debug.Log("Add FlowMosh effect to your Volume component to make Manipulation Example work");
                return;
            }

            //Activate effect
            m_Effect.active = true;
        }
        private void FixedUpdate()
        {
            //Null check
            if (volume == null)
                return;
            if (m_Effect is null)
                return;

            // Randomly change Distortion intencity value
            m_Effect.BlockSize.value = UnityEngine.Random.Range(0.00001f, .01f);
        }

    }
}
