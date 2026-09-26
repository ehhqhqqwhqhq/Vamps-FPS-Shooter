using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Vamp.Accounts
{
    /// <summary>
    /// Username/password rules. Create an asset named "VampAccountRules" in a Resources folder to override
    /// (e.g. to add reserved names) - otherwise these defaults are used. The server enforces the same rules.
    /// </summary>
    [CreateAssetMenu(menuName = "VAMP/Account Rules", fileName = "VampAccountRules")]
    public sealed class AccountRules : ScriptableObject
    {
        public int usernameMin = 3;
        public int usernameMax = 16;
        public int passwordMin = 8;
        public int passwordMax = 64;
        public List<string> reservedUsernames = new List<string>
        {
            "admin", "administrator", "moderator", "mod", "dev", "developer", "system", "vamp", "official",
            "support", "staff", "server", "root", "null", "guest"
        };

        [Header("Rate limits")]
        public int maxFailedLoginsPerUser = 5;
        public float loginWindowSeconds = 300f;
        public float loginLockoutSeconds = 60f;
        public int maxRegistrationsPerHour = 5;

        [Header("Sessions")]
        public float sessionHours = 12f;
        public float rememberMeDays = 30f;

        private static readonly Regex Allowed = new Regex("^[A-Za-z0-9_]+$");
        private static AccountRules _default;

        public static AccountRules Load()
        {
            var r = Resources.Load<AccountRules>("VampAccountRules");
            if (r != null) return r;
            if (_default == null) _default = CreateInstance<AccountRules>();
            return _default;
        }

        public static string Normalize(string username)
        {
            return (username ?? "").Trim().ToLowerInvariant();
        }

        public string ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return "ENTER A USERNAME";
            if (username.Contains(" ")) return "USERNAMES CANNOT CONTAIN SPACES";
            if (username.Length < usernameMin || username.Length > usernameMax)
                return "USERNAME MUST BE " + usernameMin + "-" + usernameMax + " CHARACTERS";
            if (!Allowed.IsMatch(username)) return "ONLY LETTERS, NUMBERS AND UNDERSCORES";
            string n = Normalize(username);
            foreach (var reserved in reservedUsernames)
                if (!string.IsNullOrEmpty(reserved) && n == reserved.Trim().ToLowerInvariant()) return "THAT USERNAME IS RESERVED";
            return null;
        }

        public string ValidatePassword(string password, string confirm)
        {
            if (string.IsNullOrEmpty(password)) return "ENTER A PASSWORD";
            if (password.Length < passwordMin) return "PASSWORD MUST BE AT LEAST " + passwordMin + " CHARACTERS";
            if (password.Length > passwordMax) return "PASSWORD MUST BE AT MOST " + passwordMax + " CHARACTERS";
            if (confirm != null && password != confirm) return "PASSWORDS DO NOT MATCH";
            return null;
        }

        public PasswordStrength Evaluate(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < passwordMin) return PasswordStrength.TooShort;
            int classes = 0;
            if (Regex.IsMatch(password, "[a-z]")) classes++;
            if (Regex.IsMatch(password, "[A-Z]")) classes++;
            if (Regex.IsMatch(password, "[0-9]")) classes++;
            if (Regex.IsMatch(password, "[^A-Za-z0-9]")) classes++;
            int score = classes + (password.Length >= 12 ? 1 : 0) + (password.Length >= 16 ? 1 : 0);
            if (score <= 1) return PasswordStrength.Weak;
            if (score == 2) return PasswordStrength.Fair;
            if (score <= 4) return PasswordStrength.Good;
            return PasswordStrength.Strong;
        }
    }
}
