using UnityEngine;
using UnityEngine.UI;
using Vamp.Accounts;
using Vamp.Core;

namespace Vamp.UI.Menus
{
    /// <summary>WELCOME TO VAMP — CREATE ACCOUNT / LOG IN / EXIT.</summary>
    public sealed class WelcomeScreen : MenuScreen
    {
        public override bool ShowTopBar { get { return false; } }

        protected override void OnBuild(RectTransform root)
        {
            var col = UIKit.Node("Welcome", root);
            col.anchorMin = new Vector2(0f, 0f); col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 0.5f);
            col.anchoredPosition = new Vector2(120f, 0f);
            col.sizeDelta = new Vector2(620f, -200f);
            UIKit.VList(col, 12f, 0, TextAnchor.MiddleLeft);

            var logo = UIKit.Label(col, "VAMP", 150, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Size(logo, 170f);
            var tag = UIKit.Label(col, "MOVE FAST. AIM FASTER. NEVER STOP MOVING.", 18, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(tag, 30f);
            UIKit.Spacer(col, 30f);
            UIKit.Size(UIKit.Label(col, "WELCOME TO VAMP", 26, UIKit.TextDim), 36f);
            UIKit.Spacer(col, 10f);
            UIKit.Button(col, "CREATE ACCOUNT", () => Host.Push(new CreateAccountScreen()), UIKit.ButtonStyle.Menu, 30, 58f);
            UIKit.Button(col, "LOG IN", () => Host.Push(new LoginScreen()), UIKit.ButtonStyle.Menu, 30, 58f);
            UIKit.Button(col, "EXIT", Quit, UIKit.ButtonStyle.Menu, 30, 58f);
            UIKit.Spacer(col, 20f);
            UIKit.Caption(col, "OFFLINE BUILD · ACCOUNTS ARE STORED ON THIS PC · ONLY A USERNAME AND PASSWORD ARE NEEDED", 12);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>LOG IN — never reveals whether the username or the password was wrong.</summary>
    public sealed class LoginScreen : MenuScreen
    {
        private InputField _user, _pass;
        private Text _error;
        private bool _remember = true;

        public override bool ShowTopBar { get { return false; } }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "LOG IN", null, 560f, 120f);
            UIKit.Caption(col, "USERNAME", 14);
            _user = UIKit.Input(col, "Username", false, 16);
            UIKit.Caption(col, "PASSWORD", 14);
            var pwRow = UIKit.Row(col, 46f, 8f);
            _pass = UIKit.Input(pwRow, "Password", true, 64);
            UIKit.Size(_pass, 46f, -1f, 1f);
            ShowHide(pwRow, _pass);

            UIKit.Toggle(col, "REMEMBER ME", _remember, v => _remember = v);
            _error = UIKit.Label(col, "", 15, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(_error, 26f);
            UIKit.Button(col, "LOG IN", Submit, UIKit.ButtonStyle.Primary, 22, 54f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);

            UIKit.OnEnter(_pass, Submit);
        }

        public override void OnShow()
        {
            if (_user != null) _user.ActivateInputField();
        }

        private void Submit()
        {
            var r = Game.Accounts.Login(_user.text, _pass.text, _remember);
            if (!r.Success)
            {
                _error.text = r.Error;
                _pass.text = "";
                Audio.AudioController.PlayUI(Audio.SfxId.UIError);
                return;
            }
            Host.ClearTo(new HomeScreen());
        }

        public static void ShowHide(Transform row, InputField field)
        {
            Button b = null;
            b = UIKit.Button(row, "SHOW", () =>
            {
                bool hidden = field.contentType == InputField.ContentType.Password;
                field.contentType = hidden ? InputField.ContentType.Standard : InputField.ContentType.Password;
                field.ForceLabelUpdate();
                UIKit.ButtonText(b).text = hidden ? "HIDE" : "SHOW";
            }, UIKit.ButtonStyle.Ghost, 14, 46f);
            UIKit.Size(b, 46f, 80f);
        }
    }

    /// <summary>
    /// CREATE ACCOUNT — username (3–16, letters/numbers/underscore, unique, not reserved), password (8–64) with
    /// strength meter, show/hide and confirmation. Server repeats all validation.
    /// </summary>
    public sealed class CreateAccountScreen : MenuScreen
    {
        private InputField _user, _pass, _confirm;
        private Text _userHint, _passHint, _strength, _error;
        private Image _strengthBar;
        private bool _remember = true;

        public override bool ShowTopBar { get { return false; } }

        protected override void OnBuild(RectTransform root)
        {
            var col = Page(root, "CREATE ACCOUNT", "ONLY A USERNAME AND PASSWORD. NO EMAIL, PHONE, REAL NAME OR BIRTHDAY.", 600f, 120f);

            UIKit.Caption(col, "USERNAME", 14);
            _user = UIKit.Input(col, "3-16 letters, numbers or _", false, 16);
            _userHint = UIKit.Label(col, "", 13, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(_userHint, 20f);

            UIKit.Caption(col, "PASSWORD", 14);
            var row = UIKit.Row(col, 46f, 8f);
            _pass = UIKit.Input(row, "8-64 characters", true, 64);
            UIKit.Size(_pass, 46f, -1f, 1f);
            LoginScreen.ShowHide(row, _pass);

            var sRow = UIKit.Row(col, 20f, 10f);
            var barBg = UIKit.Image(sRow, "StrengthBg", new Color(1f, 1f, 1f, 0.1f));
            UIKit.Size(barBg, 6f, 200f);
            _strengthBar = UIKit.Image(barBg.transform, "Fill", UIKit.Red);
            var frt = _strengthBar.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f); frt.pivot = new Vector2(0f, 0.5f);
            frt.sizeDelta = Vector2.zero;
            _strength = UIKit.Label(sRow, "", 13, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Size(_strength, -1, 200f);

            UIKit.Caption(col, "CONFIRM PASSWORD", 14);
            var row2 = UIKit.Row(col, 46f, 8f);
            _confirm = UIKit.Input(row2, "Repeat password", true, 64);
            UIKit.Size(_confirm, 46f, -1f, 1f);
            LoginScreen.ShowHide(row2, _confirm);
            _passHint = UIKit.Label(col, "", 13, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            UIKit.Size(_passHint, 20f);

            UIKit.Toggle(col, "REMEMBER ME", _remember, v => _remember = v);
            _error = UIKit.Label(col, "", 15, UIKit.Red, TextAnchor.MiddleLeft);
            UIKit.Size(_error, 24f);
            UIKit.Button(col, "CREATE ACCOUNT", Submit, UIKit.ButtonStyle.Primary, 22, 54f);
            UIKit.Button(col, "BACK", () => Host.Back(), UIKit.ButtonStyle.Ghost, 18, 44f);

            _user.onValueChanged.AddListener(_ => Validate());
            _pass.onValueChanged.AddListener(_ => Validate());
            _confirm.onValueChanged.AddListener(_ => Validate());
            UIKit.OnEnter(_confirm, Submit);
            Validate();
        }

        private void Validate()
        {
            var acc = Game.Accounts;
            string u = acc.ValidateUsername(_user.text);
            _userHint.text = string.IsNullOrEmpty(_user.text) ? "SHOWN TO OTHER PLAYERS. CAPITALISATION IS KEPT; UNIQUENESS IGNORES CASE." : (u ?? "✓ LOOKS GOOD");
            _userHint.color = u == null ? UIKit.Good : (string.IsNullOrEmpty(_user.text) ? UIKit.TextDim : UIKit.Red);

            var s = acc.EvaluateStrength(_pass.text);
            float k = s == PasswordStrength.TooShort ? 0.1f : s == PasswordStrength.Weak ? 0.3f : s == PasswordStrength.Fair ? 0.55f : s == PasswordStrength.Good ? 0.8f : 1f;
            if (string.IsNullOrEmpty(_pass.text)) k = 0f;
            _strengthBar.rectTransform.sizeDelta = new Vector2(200f * k, 0f);
            _strengthBar.color = k < 0.35f ? UIKit.Red : k < 0.7f ? new Color(1f, 0.7f, 0.2f) : UIKit.Good;
            _strength.text = string.IsNullOrEmpty(_pass.text) ? "" : "STRENGTH: " + (s == PasswordStrength.TooShort ? "TOO SHORT" : s.ToString().ToUpperInvariant());

            string p = string.IsNullOrEmpty(_confirm.text) ? null : acc.ValidatePassword(_pass.text, _confirm.text);
            _passHint.text = p ?? (string.IsNullOrEmpty(_confirm.text) ? "" : "✓ PASSWORDS MATCH");
            _passHint.color = p == null ? UIKit.Good : UIKit.Red;
        }

        private void Submit()
        {
            var r = Game.Accounts.CreateAccount(_user.text, _pass.text, _confirm.text, _remember);
            if (!r.Success)
            {
                _error.text = r.Error;
                Audio.AudioController.PlayUI(Audio.SfxId.UIError);
                return;
            }
            Game.Notifications.Push(NotificationKind.Info, "ACCOUNT CREATED", "WELCOME TO VAMP, " + r.Account.username.ToUpperInvariant());
            Host.ClearTo(new HomeScreen());
        }
    }
}
