using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SunodGame.Core;

namespace SunodGame.UI
{

    public class LoginRegisterUI : MonoBehaviour
    {
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

        void Start()
        {
            ShowLogin();

            btn_Login.onClick.AddListener(OnLoginClicked);
            btn_Register.onClick.AddListener(OnRegisterClicked);
            btn_SwitchToRegister.onClick.AddListener(ShowRegister);
            btn_SwitchToLogin.onClick.AddListener(ShowLogin);

            SetLoading(false);
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
    }
}
