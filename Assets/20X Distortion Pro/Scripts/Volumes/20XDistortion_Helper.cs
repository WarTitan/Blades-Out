using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace DistortionsPro_20X
{
    public enum maskChannelMode
    {
        alphaChannel,
        redChannel
    }
    [Serializable]
public sealed class maskChannelModeParameter : VolumeParameter<maskChannelMode> { };
}
