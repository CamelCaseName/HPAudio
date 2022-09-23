using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using Object = UnityEngine.GameObject;

namespace HPAudio
{
    public class AudioMod : MelonMod
    {
        public override void OnApplicationStart()
        {
            base.OnApplicationStart();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
        }
    }
}
