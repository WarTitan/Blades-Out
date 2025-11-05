using UnityEngine;

// FILE: IItemEffect.cs
// Simple interface for item effects driven by ItemEffectRunner.

public interface IItemEffect
{
    // duration  - how long the effect should last (seconds)
    // intensity - strength multiplier (0..1+)
    void Play(float duration, float intensity);
}
