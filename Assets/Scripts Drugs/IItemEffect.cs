using UnityEngine;

// FILE: IItemEffect.cs
// Interface for item-driven screen effects.
// The "intensity" value passed into Play() is the normalized strength
// coming from ItemDeck / PlayerItemTrays (usually 0..1).

public interface IItemEffect
{
    // duration  - how long the effect should last (seconds)
    // intensity - normalized strength (0..1)
    void Play(float duration, float intensity);
}


// BASE CLASS WITH INTENSITY CAP
//
// Derive your concrete effects from this instead of implementing
// IItemEffect directly if you want a global, per-effect cap.
//
// Example:
//   public class FracturedNoiseItemEffect : CappedItemEffectBase
//   {
//       protected override void OnPlay(float duration, float intensity)
//       {
//           // "intensity" here is already clamped to [0, maxIntensity].
//       }
//   }

public abstract class CappedItemEffectBase : MonoBehaviour, IItemEffect
{
    [Header("Item Effect Cap")]
    [Tooltip("Maximum normalized intensity allowed for this effect. " +
             "Values from the ItemDeck / PlayerItemTrays will be clamped to this.")]
    [Range(0f, 1f)]
    public float maxIntensity = 1f;

    public void Play(float duration, float intensity)
    {
        // 1) Clamp to [0, 1] just in case
        float clamped = Mathf.Clamp01(intensity);

        // 2) Apply per-effect cap
        clamped = Mathf.Min(clamped, maxIntensity);

        // 3) Call into the concrete effect implementation
        OnPlay(duration, clamped);
    }

    // Your effect scripts implement this instead of Play()
    protected abstract void OnPlay(float duration, float intensity);
}
