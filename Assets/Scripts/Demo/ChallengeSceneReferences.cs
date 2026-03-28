using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public class ChallengeSceneReferences : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] private Transform challengeHudRoot;
        [SerializeField] private Transform controlsCanvas;
        [SerializeField] private TMP_Text roundCounterText;
        [SerializeField] private TMP_Text roundObjectiveText;
        [SerializeField] private TMP_Text debugText;
        [SerializeField] private Button nextRoundButton;
        [SerializeField] private TMP_Text nextRoundButtonLabel;
        [SerializeField] private Image[] starImages = new Image[3];

        [Header("Challenge Content Roots")]
        [SerializeField] private GameObject[] challengeRoots = new GameObject[6];

        [Header("Skill Buttons")]
        [SerializeField] private OnScreenButton[] skillButtons = new OnScreenButton[6];

        [Header("Star Colors")]
        [SerializeField] private Color filledStarColor = new(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color emptyStarColor = new(1f, 1f, 1f, 0.18f);

        public Transform ChallengeHudRoot => challengeHudRoot;
        public Transform ControlsCanvas => controlsCanvas;
        public TMP_Text RoundCounterText => roundCounterText;
        public TMP_Text RoundObjectiveText => roundObjectiveText;
        public TMP_Text DebugText => debugText;
        public Button NextRoundButton => nextRoundButton;
        public TMP_Text NextRoundButtonLabel => nextRoundButtonLabel;
        public Image[] StarImages => starImages;
        public GameObject[] ChallengeRoots => challengeRoots;
        public OnScreenButton[] SkillButtons => skillButtons;
        public Color FilledStarColor => filledStarColor;
        public Color EmptyStarColor => emptyStarColor;

        public bool HasShellReferences()
        {
            return challengeHudRoot != null &&
                   challengeRoots != null &&
                   challengeRoots.Length > 0 &&
                   challengeRoots[0] != null;
        }
    }
}
