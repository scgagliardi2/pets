using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// The app's entry-point screen: Continue (if a save exists and hasn't ended), New Run,
    /// Stats. Shown whenever RunController.Screen == Home; hidden otherwise. Toggles between its
    /// own "home content" (this panel's buttons) and StatsScreenUI's content locally — that
    /// sub-navigation doesn't need to round-trip through RunController.
    /// </summary>
    public sealed class HomeScreenUI : MonoBehaviour
    {
        [SerializeField] private RunController runController;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject homeContent;
        [SerializeField] private GameObject statsContent;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button statsButton;
        [SerializeField] private StatsScreenUI statsScreenUI;

        private void Awake()
        {
            continueButton.onClick.AddListener(() => runController.ContinueRun());
            newRunButton.onClick.AddListener(() => runController.StartNewRun());
            statsButton.onClick.AddListener(() =>
            {
                homeContent.SetActive(false);
                statsContent.SetActive(true);
                statsScreenUI.Refresh();
            });
        }

        private void OnEnable()
        {
            runController.OnStateChanged += Refresh;
        }

        private void OnDisable()
        {
            runController.OnStateChanged -= Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool isHome = runController.Screen == AppScreen.Home;
            homePanel.SetActive(isHome);
            if (!isHome)
            {
                return;
            }

            homeContent.SetActive(true);
            statsContent.SetActive(false);
            continueButton.gameObject.SetActive(runController.HasContinuableRun());
        }
    }
}
