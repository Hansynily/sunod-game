using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SunodGame.Core;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    public class LoginRegisterUI : MonoBehaviour
    {
        private const string PresetRailway = TelemetryManager.BackendModeRailway;
        private const string PresetCustom = TelemetryManager.BackendModeCustom;

        [Header("Panels")]
        [SerializeField] private GameObject panelLogin;
        [SerializeField] private GameObject panelRegister;

        [Header("Login Panel")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField loginPasswordInput;
        [SerializeField] private Button         btn_Login;
        [SerializeField] private Button         btn_SwitchToRegister;
        [SerializeField] private TMP_Text       txt_ErrorLogin;

        [Header("Register Panel")]
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private Button         btn_Register;
        [SerializeField] private Button         btn_SwitchToLogin;
        [SerializeField] private TMP_Text       txt_ErrorRegister;

        [Header("Shared")]
        [SerializeField] private TMP_Text txt_Loading;

        private GameObject panelBackendSettings;
        private TMP_InputField inputBackendUrl;
        private TMP_Text txtBackendStatus;
        private Button btnLoginBackendSettings;
        private Button btnRegisterBackendSettings;
        private Button btnBackendClose;
        private Button btnBackendPresetRailway;
        private Button btnBackendPresetCustom;
        private Button btnBackendSave;
        private Button btnBackendCancel;
        private string selectedBackendPreset = PresetRailway;

        void Start()
        {
            ShowLogin();

            btn_Login.onClick.AddListener(OnLoginClicked);
            btn_Register.onClick.AddListener(OnRegisterClicked);
            btn_SwitchToRegister.onClick.AddListener(ShowRegister);
            btn_SwitchToLogin.onClick.AddListener(ShowLogin);

            BindBackendSettingsUi();
            SetLoading(false);
        }

        void OnDestroy()
        {
            if (btn_Login != null)
                btn_Login.onClick.RemoveListener(OnLoginClicked);

            if (btn_Register != null)
                btn_Register.onClick.RemoveListener(OnRegisterClicked);

            if (btn_SwitchToRegister != null)
                btn_SwitchToRegister.onClick.RemoveListener(ShowRegister);

            if (btn_SwitchToLogin != null)
                btn_SwitchToLogin.onClick.RemoveListener(ShowLogin);

            if (btnLoginBackendSettings != null)
                btnLoginBackendSettings.onClick.RemoveListener(OpenBackendSettings);

            if (btnRegisterBackendSettings != null)
                btnRegisterBackendSettings.onClick.RemoveListener(OpenBackendSettings);

            if (btnBackendClose != null)
                btnBackendClose.onClick.RemoveListener(CloseBackendSettings);

            if (btnBackendCancel != null)
                btnBackendCancel.onClick.RemoveListener(CloseBackendSettings);

            if (btnBackendPresetRailway != null)
                btnBackendPresetRailway.onClick.RemoveListener(OnRailwayPresetClicked);

            if (btnBackendPresetCustom != null)
                btnBackendPresetCustom.onClick.RemoveListener(OnCustomPresetClicked);

            if (btnBackendSave != null)
                btnBackendSave.onClick.RemoveListener(OnBackendSaveClicked);
        }

        // TAB SWITCHING

        void ShowLogin()
        {
            panelLogin.SetActive(true);
            panelRegister.SetActive(false);
            ClearErrors();
        }

        void ShowRegister()
        {
            panelLogin.SetActive(false);
            panelRegister.SetActive(true);
            ClearErrors();
        }

        // LOGIN

        void OnLoginClicked()
        {
            ClearErrors();
            if (usernameInput == null || loginPasswordInput == null)
            {
                ShowError(txt_ErrorLogin, "Login UI references are missing.");
                return;
            }

            string username = usernameInput.text;
            string password = loginPasswordInput.text;

            SetLoading(true);
            AuthManager.Instance.Login(username,
                password,
                onSuccess: () =>
                {
                    SetLoading(false);
                    SceneLoader.GoToMainMenu();
                },
                onError: (err) =>
                {
                    SetLoading(false);
                    ShowError(txt_ErrorLogin, $"Login failed: {err}");
                });
        }

        // REGISTER

        void OnRegisterClicked()
        {
            ClearErrors();
            if (registerUsernameInput == null || registerPasswordInput == null)
            {
                ShowError(txt_ErrorRegister, "Register UI refs are missing.");
                return;
            }

            string username = registerUsernameInput.text;
            string password = registerPasswordInput.text;

            SetLoading(true);
            AuthManager.Instance.Register(username,
                password,
                onSuccess: () =>
                {
                    SetLoading(false);
                    SceneLoader.GoToMainMenu();
                },
                onError: (err) =>
                {
                    SetLoading(false);
                    ShowError(txt_ErrorRegister, $"Register failed: {err}");
                });
        }

        // HELPERS

        void ShowError(TMP_Text label, string message)
        {
            if (label == null) return;
            label.text = message;
            label.gameObject.SetActive(true);
        }

        void ClearErrors()
        {
            if (txt_ErrorLogin    != null) { txt_ErrorLogin.text = "";    txt_ErrorLogin.gameObject.SetActive(false); }
            if (txt_ErrorRegister != null) { txt_ErrorRegister.text = ""; txt_ErrorRegister.gameObject.SetActive(false); }
        }

        void SetLoading(bool isLoading)
        {
            if (txt_Loading != null) txt_Loading.gameObject.SetActive(isLoading);
            if (btn_Login != null) btn_Login.interactable = !isLoading;
            if (btn_Register != null) btn_Register.interactable = !isLoading;
            if (btn_SwitchToRegister != null) btn_SwitchToRegister.interactable = !isLoading;
            if (btn_SwitchToLogin != null) btn_SwitchToLogin.interactable = !isLoading;
        }

        void BindBackendSettingsUi()
        {
            panelBackendSettings ??= FindSceneObject("Panel_BackendSettings");
            inputBackendUrl ??= FindSceneComponent<TMP_InputField>("Input_BackendUrl");
            txtBackendStatus ??= FindSceneComponent<TMP_Text>("TXT_BackendStatus");
            btnLoginBackendSettings ??= FindSceneComponent<Button>("BTN_LoginBackendSettings");
            btnRegisterBackendSettings ??= FindSceneComponent<Button>("BTN_RegisterBackendSettings");
            btnBackendClose ??= FindSceneComponent<Button>("BTN_BackendClose");
            btnBackendPresetRailway ??= FindSceneComponent<Button>("BTN_BackendPresetRailway");
            btnBackendPresetCustom ??= FindSceneComponent<Button>("BTN_BackendPresetCustom");
            btnBackendSave ??= FindSceneComponent<Button>("BTN_BackendSave");
            btnBackendCancel ??= FindSceneComponent<Button>("BTN_BackendCancel");

            if (panelBackendSettings == null)
                return;

            if (btnLoginBackendSettings != null)
                btnLoginBackendSettings.onClick.AddListener(OpenBackendSettings);

            if (btnRegisterBackendSettings != null)
                btnRegisterBackendSettings.onClick.AddListener(OpenBackendSettings);

            if (btnBackendClose != null)
                btnBackendClose.onClick.AddListener(CloseBackendSettings);

            if (btnBackendCancel != null)
                btnBackendCancel.onClick.AddListener(CloseBackendSettings);

            if (btnBackendPresetRailway != null)
                btnBackendPresetRailway.onClick.AddListener(OnRailwayPresetClicked);

            if (btnBackendPresetCustom != null)
                btnBackendPresetCustom.onClick.AddListener(OnCustomPresetClicked);

            if (btnBackendSave != null)
                btnBackendSave.onClick.AddListener(OnBackendSaveClicked);

            panelBackendSettings.SetActive(false);
            RefreshBackendSettingsView();
        }

        void OpenBackendSettings()
        {
            if (panelBackendSettings == null)
                return;

            RefreshBackendSettingsView();
            panelBackendSettings.SetActive(true);
        }

        void CloseBackendSettings()
        {
            if (panelBackendSettings != null)
                panelBackendSettings.SetActive(false);
        }

        void OnRailwayPresetClicked()
        {
            selectedBackendPreset = PresetRailway;
            string railwayUrl = TelemetryManager.Instance?.RailwayPresetUrl ?? string.Empty;
            if (inputBackendUrl != null && !string.IsNullOrWhiteSpace(railwayUrl))
                inputBackendUrl.text = railwayUrl;

            SetBackendStatus(string.IsNullOrWhiteSpace(railwayUrl) && string.IsNullOrWhiteSpace(inputBackendUrl?.text)
                ? "Railway URL is not configured yet."
                : $"Railway selected: {inputBackendUrl?.text}");
        }

        void OnCustomPresetClicked()
        {
            selectedBackendPreset = PresetCustom;
            SetBackendStatus("Custom mode selected. Enter the full backend URL.");
        }

        void OnBackendSaveClicked()
        {
            if (TelemetryManager.Instance == null)
            {
                SetBackendStatus("Backend manager is unavailable.");
                return;
            }

            bool success;
            string resolvedUrl;
            string errorMessage;

            switch (selectedBackendPreset)
            {
                case PresetRailway:
                    success = TelemetryManager.Instance.TryUseRailwayBackend(inputBackendUrl != null ? inputBackendUrl.text : string.Empty, out resolvedUrl, out errorMessage);
                    break;
                case PresetCustom:
                    success = TelemetryManager.Instance.TryUseCustomBackend(inputBackendUrl != null ? inputBackendUrl.text : string.Empty, out resolvedUrl, out errorMessage);
                    break;
                default:
                    success = TelemetryManager.Instance.TryUseCustomBackend(inputBackendUrl != null ? inputBackendUrl.text : string.Empty, out resolvedUrl, out errorMessage);
                    break;
            }

            if (!success)
            {
                SetBackendStatus(errorMessage);
                return;
            }

            if (inputBackendUrl != null)
                inputBackendUrl.text = resolvedUrl;

            SetBackendStatus($"Saved {selectedBackendPreset}: {resolvedUrl}");
            CloseBackendSettings();
        }

        void RefreshBackendSettingsView()
        {
            TelemetryManager telemetryManager = TelemetryManager.Instance;
            if (telemetryManager == null)
            {
                SetBackendStatus("Backend manager is unavailable.");
                return;
            }

            selectedBackendPreset = telemetryManager.CurrentBackendMode;

            if (inputBackendUrl != null)
                inputBackendUrl.text = telemetryManager.BaseUrl;

            SetBackendStatus($"Current: {telemetryManager.CurrentBackendMode} - {telemetryManager.BaseUrl}");
        }

        void SetBackendStatus(string message)
        {
            if (txtBackendStatus == null)
                return;

            txtBackendStatus.text = message ?? string.Empty;
        }

        GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject rootObject in gameObject.scene.GetRootGameObjects())
            {
                Transform match = FindInChildren(rootObject.transform, objectName);
                if (match != null)
                    return match.gameObject;
            }

            return null;
        }

        T FindSceneComponent<T>(string objectName) where T : Component
        {
            GameObject sceneObject = FindSceneObject(objectName);
            return sceneObject != null ? sceneObject.GetComponent<T>() : null;
        }

        Transform FindInChildren(Transform parent, string objectName)
        {
            if (parent.name == objectName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform match = FindInChildren(parent.GetChild(i), objectName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
