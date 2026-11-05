using UnityEngine.UIElements;
using SunodGame.Core;

namespace SunodGame.UI
{
    /// <summary>
    /// Settings row "Gameplay: Old / New" (vc_GameplayMode). Shown to admin accounts only;
    /// everyone else plays Old and never sees it.
    /// </summary>
    public partial class MainMenuUIToolkit
    {
        private VisualElement _gameplayRow;
        private Button _btnGameplayOld, _btnGameplayNew;

        private void BindGameplay(VisualElement root)
        {
            _gameplayRow = root.Q<VisualElement>("settings-gameplay-row");
            _btnGameplayOld = root.Q<Button>("btn-gameplay-old");
            _btnGameplayNew = root.Q<Button>("btn-gameplay-new");
            if (_btnGameplayOld != null) _btnGameplayOld.clicked += () => SetGameplay(vc_GameplayMode.Old);
            if (_btnGameplayNew != null) _btnGameplayNew.clicked += () => SetGameplay(vc_GameplayMode.New);
            RefreshGameplayChoice();
        }

        private void SetGameplay(string mode)
        {
            if (!vc_GameplayMode.CanChoose) return;
            vc_GameplayMode.Chosen = mode;
            vc_Sfx.Play("click", 0.6f);
            RefreshGameplayChoice();
        }

        private void RefreshGameplayChoice()
        {
            bool admin = vc_GameplayMode.CanChoose;
            _gameplayRow?.SetDisplay(admin);
            bool isNew = vc_GameplayMode.Current == vc_GameplayMode.New;
            _btnGameplayOld?.EnableInClassList("settings-choice-btn--on", !isNew);
            _btnGameplayNew?.EnableInClassList("settings-choice-btn--on", isNew);
        }
    }
}
