using System;
using System.Collections.Generic;

namespace Pets.Simulation
{
    [Serializable]
    public sealed class AbilityData
    {
        public TriggerType Trigger;
        public List<EffectData> Effects = new List<EffectData>();
    }
}
