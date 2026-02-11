using UnityEngine;

namespace Game.Runtime
{
    // Forces Mirror runtime assembly into player compile graph.
    internal sealed class MirrorAnchor : MonoBehaviour
    {
        void Awake()
        {
            // Reference a core Mirror type so the assembly is pulled in.
            if (Mirror.Transport.active == null)
            {
                return;
            }
        }
    }
}
