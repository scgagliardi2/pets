using UnityEngine;
using UnityEngine.UI;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Settings screen, reached from Home and the Ingame Menu (its Back button returns
    /// to whichever opened it — see SceneNavigator.GoToSettings). One setting so far: whether
    /// battles autoplay (GameSettings.AutoplayBattles), shown as an On/Off button rather than a
    /// checkbox so it uses the same button art as everything else.</summary>
    public sealed class SettingsScreenController : MonoBehaviour
    {
        [SerializeField] private Button autoplayToggleButton;

        private void Start() => Refresh();

        public void OnAutoplayToggleClicked()
        {
            GameSettings.AutoplayBattles = !GameSettings.AutoplayBattles;
            Refresh();
        }

        private void Refresh()
        {
            bool on = GameSettings.AutoplayBattles;
            var button = autoplayToggleButton.GetComponent<UiButton>();
            button.Text = on ? "On" : "Off";
            button.Style = on ? Theme.ButtonStyle.Confirm : Theme.ButtonStyle.Secondary;
        }
    }
}
