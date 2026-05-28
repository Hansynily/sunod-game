using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using SunodGame.Core;
using SunodGame.Models;
using SunodGame.Telemetry;

namespace SunodGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LoginRegisterUIToolkit : MonoBehaviour
    {
        private const string PresetRailway = TelemetryManager.BackendModeRailway;
        private const string PresetCustom  = TelemetryManager.BackendModeCustom;

        [Header("Developer")]
        [SerializeField] private bool showDeveloperBackendControls = false;

        // Panels
        private VisualElement _panelLogin;
        private VisualElement _panelRegister;
        private VisualElement _panelBackend;

        // Login
        private TextField _inputUsername;
        private TextField _inputPassword;
        private Button    _btnLogin;
        private Button    _btnSwitchToRegister;
        private Label     _lblErrorLogin;
        private Label     _lblLoading;

        // Register
        private TextField    _inputRegName;
        private TextField    _inputRegUsername;
        private TextField    _inputRegEmail;
        private TextField    _inputRegPassword;
        private DropdownField _dropdownGender;
        private DropdownField _dpYear;
        private DropdownField _dpMonth;
        private DropdownField _dpDay;
        private Button        _btnRegister;
        private Button        _btnSwitchToLogin;
        private Label         _lblErrorRegister;

        // Backend Settings
        private TextField     _inputBackendUrl;
        private Label         _lblBackendStatus;
        private Button        _btnBackendClose;
        private Button        _btnBackendCancel;
        private Button        _btnBackendSave;
        private Button        _btnPresetRailway;
        private Button        _btnPresetCustom;
        private Button        _btnDevBackend;

        private string _selectedBackendPreset = PresetRailway;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            // Panels
            _panelLogin    = root.Q<VisualElement>("panel-login");
            _panelRegister = root.Q<VisualElement>("panel-register");
            _panelBackend  = root.Q<VisualElement>("panel-backend");

            // Login
            _inputUsername        = root.Q<TextField>("input-username");
            _inputPassword        = root.Q<TextField>("input-password");
            _btnLogin             = root.Q<Button>("btn-login");
            _btnSwitchToRegister  = root.Q<Button>("btn-switch-to-register");
            _lblErrorLogin        = root.Q<Label>("lbl-error-login");
            _lblLoading           = root.Q<Label>("lbl-loading");

            // Register
            _inputRegName     = root.Q<TextField>("input-reg-name");
            _inputRegUsername = root.Q<TextField>("input-reg-username");
            _inputRegEmail    = root.Q<TextField>("input-reg-email");
            _inputRegPassword = root.Q<TextField>("input-reg-password");
            _dropdownGender   = root.Q<DropdownField>("dropdown-gender");
            _dpYear           = root.Q<DropdownField>("dp-year");
            _dpMonth          = root.Q<DropdownField>("dp-month");
            _dpDay            = root.Q<DropdownField>("dp-day");
            _btnRegister      = root.Q<Button>("btn-register");
            _btnSwitchToLogin = root.Q<Button>("btn-switch-to-login");
            _lblErrorRegister = root.Q<Label>("lbl-error-register");

            // Backend
            _inputBackendUrl  = root.Q<TextField>("input-backend-url");
            _lblBackendStatus = root.Q<Label>("lbl-backend-status");
            _btnBackendClose  = root.Q<Button>("btn-backend-close");
            _btnBackendCancel = root.Q<Button>("btn-backend-cancel");
            _btnBackendSave   = root.Q<Button>("btn-backend-save");
            _btnPresetRailway = root.Q<Button>("btn-preset-railway");
            _btnPresetCustom  = root.Q<Button>("btn-preset-custom");
            _btnDevBackend    = root.Q<Button>("btn-dev-backend");

            // Placeholder + password masking
            SetupPlaceholder(_inputUsername,    "Username");
            SetupPlaceholder(_inputPassword,    "Password",          isPassword: true);
            SetupPlaceholder(_inputRegName,     "Full name...");
            SetupPlaceholder(_inputRegUsername, "Choose username...");
            SetupPlaceholder(_inputRegEmail,    "Choose email...");
            SetupPlaceholder(_inputRegPassword, "Choose password...", isPassword: true);

            PopulateDropdowns();

            // Wire buttons
            _btnLogin?            .RegisterCallback<ClickEvent>(OnLoginClicked);
            _btnSwitchToRegister? .RegisterCallback<ClickEvent>(_ => ShowRegister());
            _btnRegister?         .RegisterCallback<ClickEvent>(OnRegisterClicked);
            _btnSwitchToLogin?    .RegisterCallback<ClickEvent>(_ => ShowLogin());
            _btnBackendClose?     .RegisterCallback<ClickEvent>(_ => CloseBackendSettings());
            _btnBackendCancel?    .RegisterCallback<ClickEvent>(_ => CloseBackendSettings());
            _btnBackendSave?      .RegisterCallback<ClickEvent>(_ => OnBackendSaveClicked());
            _btnPresetRailway?    .RegisterCallback<ClickEvent>(_ => OnRailwayPresetClicked());
            _btnPresetCustom?     .RegisterCallback<ClickEvent>(_ => OnCustomPresetClicked());
            _btnDevBackend?       .RegisterCallback<ClickEvent>(_ => OpenBackendSettings());

            // Initial state
            ShowLogin();
            SetLoading(false);
            ApplyDevBackendVisibility();
        }

        private void OnDisable()
        {
            _btnLogin?            .UnregisterCallback<ClickEvent>(OnLoginClicked);
            _btnSwitchToRegister? .UnregisterCallback<ClickEvent>(_ => ShowRegister());
            _btnRegister?         .UnregisterCallback<ClickEvent>(OnRegisterClicked);
            _btnSwitchToLogin?    .UnregisterCallback<ClickEvent>(_ => ShowLogin());
            _btnBackendClose?     .UnregisterCallback<ClickEvent>(_ => CloseBackendSettings());
            _btnBackendCancel?    .UnregisterCallback<ClickEvent>(_ => CloseBackendSettings());
            _btnBackendSave?      .UnregisterCallback<ClickEvent>(_ => OnBackendSaveClicked());
            _btnPresetRailway?    .UnregisterCallback<ClickEvent>(_ => OnRailwayPresetClicked());
            _btnPresetCustom?     .UnregisterCallback<ClickEvent>(_ => OnCustomPresetClicked());
            _btnDevBackend?       .UnregisterCallback<ClickEvent>(_ => OpenBackendSettings());
        }

        // ============================
        // Panel Switching
        // ============================

        private void ShowLogin()
        {
            _panelLogin?   .RemoveFromClassList("hidden");
            _panelRegister?.AddToClassList("hidden");
            ClearErrors();
        }

        private void ShowRegister()
        {
            _panelLogin?   .AddToClassList("hidden");
            _panelRegister?.RemoveFromClassList("hidden");
            ClearErrors();
        }

        // ============================
        // Login
        // ============================

        private void OnLoginClicked(ClickEvent _)
        {
            ClearErrors();

            if (AuthManager.Instance == null)
            {
                ShowError(_lblErrorLogin, "Auth manager is unavailable.");
                return;
            }

            string username = GetFieldValue(_inputUsername);
            string password = GetFieldValue(_inputPassword);

            SetLoading(true);
            AuthManager.Instance.Login(
                username,
                password,
                onResolved: (auth) => { SetLoading(false); HandleLoginResolved(auth); },
                onError:    (err)  => { SetLoading(false); ShowError(_lblErrorLogin, err); });
        }

        private void HandleLoginResolved(AuthResponse auth)
        {
            if (auth == null)
            {
                ShowError(_lblErrorLogin, "Backend auth response could not be read.");
                return;
            }
            if (!auth.can_login)
            {
                ShowError(_lblErrorLogin, GetAuthMessage(auth, "This account cannot log in yet."));
                return;
            }
            if (string.IsNullOrWhiteSpace(auth.username) || string.IsNullOrWhiteSpace(auth.player_id))
            {
                ShowError(_lblErrorLogin, "Backend auth response is missing some IDs.");
                return;
            }

            SessionState.Instance?.SetAuthenticatedUser(
                auth.username, auth.player_id, auth.access_token,
                auth.name, auth.birthdate, auth.gender, auth.tutorial_completed);
            TelemetryManager.Instance?.TagSessionStart();
            SceneLoader.GoToMainMenu();
        }

        // ============================
        // Register
        // ============================

        private void OnRegisterClicked(ClickEvent _)
        {
            ClearErrors();

            if (AuthManager.Instance == null)
            {
                ShowError(_lblErrorRegister, "Auth manager is unavailable.");
                return;
            }

            string name      = GetFieldValue(_inputRegName).Trim();
            string username  = GetFieldValue(_inputRegUsername).Trim();
            string email     = GetFieldValue(_inputRegEmail).Trim();
            string password  = GetFieldValue(_inputRegPassword).Trim();
            string birthdate = GetBirthdateString();
            string gender    = NormalizeGender();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError(_lblErrorRegister, "Enter your full name.");
                return;
            }
            if (!LooksLikeBirthdate(birthdate))
            {
                ShowError(_lblErrorRegister, "Select a valid birthdate.");
                return;
            }
            if (string.IsNullOrWhiteSpace(gender))
            {
                ShowError(_lblErrorRegister, "Select a gender option.");
                return;
            }
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                ShowError(_lblErrorRegister, "Enter a valid email address.");
                return;
            }
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError(_lblErrorRegister, "Choose a username.");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(_lblErrorRegister, "Choose a password.");
                return;
            }

            SetLoading(true);
            AuthManager.Instance.Register(
                name, birthdate, gender, username, password, email,
                onResolved: (auth) =>
                {
                    SetLoading(false);
                    ShowLogin();
                    ShowError(_lblErrorLogin, GetAuthMessage(auth, "Account created. Wait for admin approval."));
                },
                onError: (err) => { SetLoading(false); ShowError(_lblErrorRegister, err); });
        }

        // ============================
        // Birthdate Date Picker
        // ============================

        private void PopulateDropdowns()
        {
            // Gender
            if (_dropdownGender != null)
            {
                _dropdownGender.choices = new List<string>
                    { "Select Gender", "Male", "Female", "Other", "Prefer not to say" };
                _dropdownGender.index = 0;
            }

            // Year: current year down to 1930
            if (_dpYear != null)
            {
                var years = new List<string> { "YYYY" };
                for (int y = DateTime.Now.Year; y >= 1930; y--)
                    years.Add(y.ToString());
                _dpYear.choices = years;
                _dpYear.index   = 0;
            }

            // Month: 01–12
            if (_dpMonth != null)
            {
                _dpMonth.choices = new List<string>
                    { "MM", "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12" };
                _dpMonth.index = 0;
            }

            // Day: 01–31
            if (_dpDay != null)
            {
                var days = new List<string> { "DD" };
                for (int d = 1; d <= 31; d++)
                    days.Add(d.ToString("D2"));
                _dpDay.choices = days;
                _dpDay.index   = 0;
            }
        }

        private string GetBirthdateString()
        {
            string yr = _dpYear?.value  ?? string.Empty;
            string mo = _dpMonth?.value ?? string.Empty;
            string dy = _dpDay?.value   ?? string.Empty;

            if (yr == "YYYY" || mo == "MM" || dy == "DD" ||
                string.IsNullOrEmpty(yr) || string.IsNullOrEmpty(mo) || string.IsNullOrEmpty(dy))
                return string.Empty;

            return $"{yr}-{mo}-{dy}";
        }

        private string NormalizeGender()
        {
            return (_dropdownGender?.value) switch
            {
                "Male"              => "male",
                "Female"            => "female",
                "Other"             => "other",
                "Prefer not to say" => "prefer_not_to_say",
                _                   => null,
            };
        }

        // ============================
        // Backend Settings
        // ============================

        private void ApplyDevBackendVisibility()
        {
            if (_btnDevBackend == null) return;
            if (showDeveloperBackendControls)
                _btnDevBackend.RemoveFromClassList("hidden");
            else
                _btnDevBackend.AddToClassList("hidden");
        }

        private void OpenBackendSettings()
        {
            if (_panelBackend == null) return;
            RefreshBackendSettingsView();
            _panelBackend.RemoveFromClassList("hidden");
        }

        private void CloseBackendSettings()
        {
            _panelBackend?.AddToClassList("hidden");
        }

        private void OnRailwayPresetClicked()
        {
            _selectedBackendPreset = PresetRailway;
            string railwayUrl = TelemetryManager.Instance?.RailwayPresetUrl ?? string.Empty;
            if (_inputBackendUrl != null && !string.IsNullOrWhiteSpace(railwayUrl))
                _inputBackendUrl.value = railwayUrl;

            SetBackendStatus(string.IsNullOrWhiteSpace(railwayUrl)
                ? "Railway URL is not configured yet."
                : $"Railway selected: {_inputBackendUrl?.value}");

            _btnPresetRailway?.AddToClassList("btn-preset-active");
            _btnPresetCustom? .RemoveFromClassList("btn-preset-active");
        }

        private void OnCustomPresetClicked()
        {
            _selectedBackendPreset = PresetCustom;
            SetBackendStatus("Custom mode selected. Enter the full backend URL.");
            _btnPresetCustom? .AddToClassList("btn-preset-active");
            _btnPresetRailway?.RemoveFromClassList("btn-preset-active");
        }

        private void OnBackendSaveClicked()
        {
            if (TelemetryManager.Instance == null)
            {
                SetBackendStatus("Backend manager is unavailable.");
                return;
            }

            string inputUrl = _inputBackendUrl?.value ?? string.Empty;
            bool success;
            string resolvedUrl;
            string errorMessage;

            if (_selectedBackendPreset == PresetRailway)
                success = TelemetryManager.Instance.TryUseRailwayBackend(inputUrl, out resolvedUrl, out errorMessage);
            else
                success = TelemetryManager.Instance.TryUseCustomBackend(inputUrl, out resolvedUrl, out errorMessage);

            if (!success)
            {
                SetBackendStatus(errorMessage);
                return;
            }

            if (_inputBackendUrl != null)
                _inputBackendUrl.value = resolvedUrl;

            SetBackendStatus($"Saved {_selectedBackendPreset}: {resolvedUrl}");
            CloseBackendSettings();
        }

        private void RefreshBackendSettingsView()
        {
            var tm = TelemetryManager.Instance;
            if (tm == null)
            {
                SetBackendStatus("Backend manager is unavailable.");
                return;
            }

            _selectedBackendPreset = tm.CurrentBackendMode;
            if (_inputBackendUrl != null)
                _inputBackendUrl.value = tm.BaseUrl;

            SetBackendStatus($"Current: {tm.CurrentBackendMode} — {tm.BaseUrl}");

            if (_selectedBackendPreset == PresetRailway)
            {
                _btnPresetRailway?.AddToClassList("btn-preset-active");
                _btnPresetCustom? .RemoveFromClassList("btn-preset-active");
            }
            else
            {
                _btnPresetCustom? .AddToClassList("btn-preset-active");
                _btnPresetRailway?.RemoveFromClassList("btn-preset-active");
            }
        }

        private void SetBackendStatus(string message)
        {
            if (_lblBackendStatus != null)
                _lblBackendStatus.text = message ?? string.Empty;
        }

        // ============================
        // Helpers
        // ============================

        private void ShowError(Label label, string message)
        {
            if (label == null) return;
            label.text = message;
            label.RemoveFromClassList("hidden");
        }

        private void ClearErrors()
        {
            _lblErrorLogin?    .AddToClassList("hidden");
            _lblErrorRegister? .AddToClassList("hidden");
        }

        private void SetLoading(bool isLoading)
        {
            if (_lblLoading != null)
            {
                if (isLoading) _lblLoading.RemoveFromClassList("hidden");
                else           _lblLoading.AddToClassList("hidden");
            }
            _btnLogin?    .SetEnabled(!isLoading);
            _btnRegister? .SetEnabled(!isLoading);
            _btnSwitchToRegister?.SetEnabled(!isLoading);
            _btnSwitchToLogin?   .SetEnabled(!isLoading);
        }

        private static string GetAuthMessage(AuthResponse auth, string fallback)
        {
            return auth != null && !string.IsNullOrWhiteSpace(auth.message)
                ? auth.message
                : fallback;
        }

        private static bool LooksLikeBirthdate(string birthdate)
        {
            if (string.IsNullOrWhiteSpace(birthdate)) return false;
            return DateTime.TryParseExact(
                birthdate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        // ============================
        // Placeholder Helpers
        // ============================

        private static void SetupPlaceholder(TextField field, string placeholder, bool isPassword = false)
        {
            if (field == null) return;

            // Treat empty or matching-placeholder value as placeholder state
            bool showPlaceholder = string.IsNullOrEmpty(field.value) || field.value == placeholder;
            if (showPlaceholder)
            {
                field.value = placeholder;
                field.AddToClassList("is-placeholder");
                field.isPasswordField = false;
            }
            else if (isPassword)
            {
                field.isPasswordField = true;
            }

            field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (!field.ClassListContains("is-placeholder")) return;
                field.value = string.Empty;
                field.RemoveFromClassList("is-placeholder");
                if (isPassword) field.isPasswordField = true;
            });

            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!string.IsNullOrEmpty(field.value)) return;
                field.value = placeholder;
                field.AddToClassList("is-placeholder");
                if (isPassword) field.isPasswordField = false;
            });
        }

        // Returns empty string if field is in placeholder state
        private static string GetFieldValue(TextField field)
        {
            if (field == null) return string.Empty;
            return field.ClassListContains("is-placeholder") ? string.Empty : field.value;
        }
    }
}
