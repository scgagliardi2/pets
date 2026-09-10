using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>Game Over / Victory panel — shown on RunController.OnRunEnded.</summary>
    public sealed class RunEndUI : MonoBehaviour
    {
        [SerializeField] private RunController runController;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button homeButton;

        private void Awake()
        {
            panel.SetActive(false);
            newRunButton.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                runController.StartNewRun();
            });
            homeButton.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                runController.GoHome();
            });
        }

        private void OnEnable()
        {
            runController.OnRunEnded += Show;
        }

        private void OnDisable()
        {
            runController.OnRunEnded -= Show;
        }

        private void Show(bool victory)
        {
            panel.SetActive(true);
            titleText.text = victory ? "Victory! You survived the run." : "Game Over";
        }
    }
}
