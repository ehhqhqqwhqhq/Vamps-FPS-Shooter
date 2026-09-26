using UnityEngine;
using Vamp.Core;

namespace Vamp.Online
{
    /// <summary>Drives <see cref="OnlineService.Tick"/> every frame (lives on the persistent network object).</summary>
    public sealed class OnlineRunner : MonoBehaviour
    {
        private void Update()
        {
            var o = Game.Online as OnlineService;
            if (o != null) o.Tick();
        }
    }
}
