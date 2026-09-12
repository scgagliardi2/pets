using UnityEngine;
using UnityEngine.UI;

namespace Pets.Gameplay
{
    /// <summary>The Home screen's only piece of state: whether there's a run to go back to.
    /// "Continue Run" is hidden outright rather than shown disabled, since on a first launch — the
    /// common case — a greyed-out button would just be a question the player can't answer.
    ///
    /// A run currently only survives in memory (ActiveRun, cleared by "New Game"), so this is
    /// really "you left a run open this session"; it becomes a genuine resume once the local save
    /// layer exists (PLAN.md Phase 2) and this screen can ask a save file instead.</summary>
    public sealed class HomeScreenController : MonoBehaviour
    {
        [SerializeField] private Button continueButton;

        private void Start()
        {
            continueButton.gameObject.SetActive(ActiveRun.HasRun);
        }
    }
}
