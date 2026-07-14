// FiberNodeConfig.cs
using UnityEngine;

namespace FiberWebGLSDK
{
    [CreateAssetMenu(fileName = "FiberNodeConfig", menuName = "Fiber/Node Config")]
    public class FiberNodeConfig : ScriptableObject
    {
        [Tooltip("The Fiber node's config.yml (e.g. testnet config).")]
        public TextAsset nodeConfig;
    }
}