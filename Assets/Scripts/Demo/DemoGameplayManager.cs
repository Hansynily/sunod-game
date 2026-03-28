// Active demo flow only. Parked thesis-future systems remain in the repo but are not part of this runtime path.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager : MonoBehaviour
    {
        private enum CatState
        {
            Idle,
            Fleeing,
            Following,
            Frozen
        }

        private class PawPrintData
        {
            public GameObject go;
            public SpriteRenderer renderer;
            public float expiresAt;
        }

        private const string DemoSceneName = "DemoPlayScene";
        private const float ToastDuration = 1.5f;
        private const float PawPrintLifetime = 3f;
        private const float PawPrintInterval = 0.5f;
        private const float RiverWorldX = 8f;
        private const float CatSpawnWorldX = 11.5f;
        private const float RiverWidth = 1.8f;
        private const float BridgeHeight = 0.5f;
        private const float BuildZoneWidth = 1.6f;
        private const float MimicFollowDuration = 0.5f;

        private static Sprite _whiteSprite;
        private static bool _sceneHookRegistered;

        private readonly bool[] _collected = new bool[6];
        private readonly int[] _skillUseCount = new int[6];
        private readonly int[] _firstUseOrder = new int[6];
        private readonly OnScreenButton[] _slotButtons = new OnScreenButton[4];
        private readonly Image[] _slotButtonImages = new Image[4];
        private readonly TMP_Text[] _slotButtonLabels = new TMP_Text[4];
        private readonly bool[] _slotUnlocked = new bool[4];
        private readonly int[] _slotSkillIndex = { -1, -1, -1, -1 };
        private readonly InputAction[] _slotActions = new InputAction[4];
        private readonly List<float> _bondExpirations = new();
        private readonly List<PawPrintData> _pawPrints = new();
        private readonly List<GameObject> _planDots = new();

        private DemoSceneReferences _sceneReferences;
        private AudioSource _audioSource;
        private AudioClip _mimicClip;
        private Transform _player;
        private Transform _contentRoot;
        private GameObject _cat;
        private SpriteRenderer _catRenderer;
        private BoxCollider2D _riverBlocker;
        private BoxCollider2D _buildZone;
        private GameObject _bridge;
        private CatState _catState = CatState.Idle;
        private Rect _worldBounds;
        private Rect _catBounds;
        private Vector3 _riverCenter;

        private TMP_Text _toastText;
        private TMP_Text _objectiveText;
        private Image _xrayOverlay;
        private GameObject _winPanel;
        private Button _continueButton;
        private Coroutine _toastRoutine;

        private bool _initialized;
        private bool _buildUsed;
        private bool _trackActive;
        private bool _planUnlocked;
        private bool _winShown;
        private bool _submittingResults;
        private bool _useChallengeSession;
        private int _collectedSkillCount;
        private int _nextUseOrder;

        private float _catFollowUntil;
        private float _catFrozenUntil;
        private float _nextPawPrintAt;
        private float _nextPlanUpdateAt;
        private float _nextRequirementToastAt;

        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;
                Texture2D tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                return _whiteSprite;
            }
        }
    }
}
