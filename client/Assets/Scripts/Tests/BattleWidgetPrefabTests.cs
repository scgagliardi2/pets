using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Tests
{
    /// <summary>The battle screen's two widget prefabs — BattleStatsBox and BattlePartySlot — are
    /// wired, and their views do what the battle screen relies on.</summary>
    public sealed class BattleWidgetPrefabTests
    {
        private const string StatsBoxPath = "Assets/Prefabs/UI/BattleStatsBox.prefab";
        private const string PartySlotPath = "Assets/Prefabs/UI/BattlePartySlot.prefab";

        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        private T Instantiate<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Prefab missing at {path} - run Pets > Build UI Prefabs.");
            instance = Object.Instantiate(prefab);
            var view = instance.GetComponent<T>();
            Assert.IsNotNull(view, $"{path} has no {typeof(T).Name}");
            return view;
        }

        [Test]
        public void StatsBox_IsWired_WithTheBarsNestedInside()
        {
            var box = Instantiate<BattleStatsBoxView>(StatsBoxPath);

            Assert.IsNotNull(box.FirstType);
            Assert.IsNotNull(box.SecondType);
            Assert.IsNotNull(box.NameText);
            Assert.IsNotNull(box.AttackText);
            Assert.IsNotNull(box.Group);
            Assert.IsNotNull(box.HealthBar, "the HealthBar prefab should be nested in the box");
            Assert.IsNotNull(box.HealthBar.Fill);
            Assert.IsNotNull(box.SpeedBar, "the SpeedBar prefab should be nested in the box");
            Assert.IsTrue(box.transform.IsChildOf(instance.transform) && box.HealthBar.transform.IsChildOf(box.transform));
        }

        [Test]
        public void StatsBox_Show_FillsInTheMon()
        {
            var box = Instantiate<BattleStatsBoxView>(StatsBoxPath);

            box.Show("Bulbasaur", PokemonType.Grass, true, PokemonType.Poison, attack: 49, speed: 45, speedMax: 200);

            Assert.IsFalse(box.IsEmpty);
            Assert.AreEqual("Bulbasaur", box.NameText.text);
            Assert.AreEqual("49", box.AttackText.text);
            Assert.AreEqual(PokemonType.Grass, box.FirstType.Type);
            Assert.AreEqual(PokemonType.Poison, box.SecondType.Type);
            Assert.IsTrue(box.SecondType.gameObject.activeSelf);
            Assert.AreEqual(45, box.SpeedBar.Value);
            Assert.AreEqual(1f, box.Group.alpha);
        }

        [Test]
        public void StatsBox_SingleType_HidesTheSecondBadge_AndSlidesTheNameOver()
        {
            var box = Instantiate<BattleStatsBoxView>(StatsBoxPath);

            box.Show("Squirtle", PokemonType.Water, true, PokemonType.Normal, 48, 43, 200);
            float dualLeft = box.NameText.rectTransform.offsetMin.x;
            box.Show("Squirtle", PokemonType.Water, false, PokemonType.Normal, 48, 43, 200);

            Assert.IsFalse(box.SecondType.gameObject.activeSelf);
            Assert.Less(box.NameText.rectTransform.offsetMin.x, dualLeft);
        }

        [Test]
        public void StatsBox_SetEmpty_ShowsOnlyTheLabel()
        {
            var box = Instantiate<BattleStatsBoxView>(StatsBoxPath);

            box.SetEmpty("No Support");

            Assert.IsTrue(box.IsEmpty);
            Assert.AreEqual("No Support", box.NameText.text);
            Assert.IsFalse(box.HealthBar.gameObject.activeSelf);
            Assert.IsFalse(box.SpeedBar.gameObject.activeSelf);
            Assert.IsFalse(box.AttackText.gameObject.activeSelf);
            Assert.Less(box.Group.alpha, 1f);
        }

        [Test]
        public void PartySlot_FramesTheLeadGoldAndTheSupportBlue()
        {
            var slot = Instantiate<BattlePartySlotView>(PartySlotPath);
            slot.SetMon(null, "Charmander");

            slot.SetRole(PartySlotRole.Lead);
            Assert.AreSame(Theme.SlotGoldSprite, slot.Frame.sprite);
            slot.SetRole(PartySlotRole.Support);
            Assert.AreSame(Theme.SlotBlueSprite, slot.Frame.sprite);
            slot.SetRole(PartySlotRole.Reserve);
            Assert.AreSame(Theme.SlotDarkSprite, slot.Frame.sprite);
            Assert.AreEqual(1f, slot.Group.alpha);

            Assert.IsNotNull(Theme.SlotGoldSprite, "the frame art should load");
            Assert.AreNotSame(Theme.SlotGoldSprite, Theme.SlotBlueSprite);
        }

        [Test]
        public void PartySlot_FaintedIsDimmed_AndEmptyShowsNothing()
        {
            var slot = Instantiate<BattlePartySlotView>(PartySlotPath);
            slot.SetMon(null, "Charmander");

            slot.SetRole(PartySlotRole.Fainted);
            Assert.Less(slot.Group.alpha, 1f);
            Assert.AreEqual("Charmander", slot.NameText.text, "a fainted mon is still named");

            slot.SetRole(PartySlotRole.Empty);
            Assert.AreEqual(string.Empty, slot.NameText.text);
            Assert.IsFalse(slot.Portrait.enabled);
        }

        [Test]
        public void PartySlot_HealthDrainsOverTheDuration()
        {
            var slot = Instantiate<BattlePartySlotView>(PartySlotPath);
            slot.SetHealthFraction(1f);

            slot.AnimateHealthFraction(0.5f, 2f);
            Assert.AreEqual(0.5f, slot.HealthFraction, "the target is known straight away");
            Assert.AreEqual(1f, slot.Fill.rectTransform.anchorMax.x, 0.0001f);

            slot.Advance(1f);
            Assert.AreEqual(0.75f, slot.Fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.IsTrue(slot.IsAnimating);

            slot.Advance(1f);
            Assert.AreEqual(0.5f, slot.Fill.rectTransform.anchorMax.x, 0.0001f);
            Assert.IsFalse(slot.IsAnimating);
        }

        [Test]
        public void ValueTween_DefaultIsFinished_AndZeroDurationIsImmediate()
        {
            Assert.IsFalse(default(ValueTween).IsRunning);

            var tween = new ValueTween(10f, 0f, 0f);
            Assert.IsFalse(tween.IsRunning);
            Assert.AreEqual(0f, tween.Value);
        }
    }
}
