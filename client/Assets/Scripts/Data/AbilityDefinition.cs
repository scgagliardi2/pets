using System;
using System.Collections.Generic;
using UnityEngine;
using Pets.Simulation;

namespace Pets.Data
{
    /// <summary>
    /// One reusable trigger + effect-list combo. See content-schema.md §2.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "Pets/Ability Definition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public TriggerType Trigger;
        public List<EffectDefinition> Effects = new List<EffectDefinition>();
    }

    [Serializable]
    public struct EffectDefinition
    {
        public EffectType Type;
        public TargetSelector Target;
        public int Amount;

        [Tooltip("Only used when Type == Summon — the creature spawned into the fainted creature's slot.")]
        public CreatureDefinition SummonTemplate;
    }
}
