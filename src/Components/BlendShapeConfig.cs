using System;

namespace Cavi.ChillWithAnyone.Components
{
    [Serializable]
    public class BlendShapeConfigItem
    {
        public string sourceName;
        public float multiplier = 1.0f;
        public float upperThreshold = 100.0f;
        public float lowerThreshold = 0.0f;
        public bool isInverted = false;
        public bool isDisabled = false;
    }

    [Serializable]
    public class GlobalCategoryConfig
    {
        public bool enabled = false;
        public float multiplier = 1.0f;
        public float upperThreshold = 100.0f;
        public float lowerThreshold = 0.0f;
        public bool isInverted = false;
        public bool isDisabled = false;
    }


    [Serializable]
    public class BlendShapeConfigRoot
    {
        public BlendShapeConfigItem[] config;
        

        public GlobalCategoryConfig mouthGlobal = new GlobalCategoryConfig();
        public GlobalCategoryConfig eyeGlobal = new GlobalCategoryConfig();

    }
}