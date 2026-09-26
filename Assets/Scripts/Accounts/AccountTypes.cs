using System;
using System.Collections.Generic;

namespace Vamp.Accounts
{
    public enum AccountStatus { Active, Suspended, Banned, Deleted }

    /// <summary>Matches the spec's `accounts` table. The id is permanent; the username is only the public identity.</summary>
    [Serializable]
    public sealed class AccountRecord
    {
        public string id;
        public string username;
        public string username_normalized;
        public string password_hash;
        public string created_at;
        public string last_login;
        public AccountStatus account_status = AccountStatus.Active;
    }

    [Serializable]
    public sealed class SessionRecord
    {
        public string account_id;
        /// <summary>SHA-256 of the session token. The raw token is never stored in the database.</summary>
        public string token_hash;
        public string created_at;
        public string expires_at;
        public bool remember_me;
        public bool revoked;
    }

    [Serializable]
    public sealed class AccountsDatabase
    {
        public int version = 1;
        public List<AccountRecord> accounts = new List<AccountRecord>();
        public List<SessionRecord> sessions = new List<SessionRecord>();
    }

    public struct AccountResult
    {
        public bool Success;
        public string Error;
        public AccountRecord Account;

        public static AccountResult Ok(AccountRecord a) { return new AccountResult { Success = true, Account = a }; }
        public static AccountResult Fail(string error) { return new AccountResult { Success = false, Error = error }; }
    }

    public enum PasswordStrength { TooShort, Weak, Fair, Good, Strong }

    public interface IAccountService
    {
        AccountRecord Current { get; }
        bool IsLoggedIn { get; }
        bool IsOnline { get; }

        event Action<AccountRecord> LoggedIn;
        event Action LoggedOut;
        event Action<AccountRecord> AccountCreated;

        /// <summary>Client-side checks for instant UI feedback. The service repeats them authoritatively.</summary>
        string ValidateUsername(string username);
        string ValidatePassword(string password, string confirm);
        PasswordStrength EvaluateStrength(string password);

        AccountResult CreateAccount(string username, string password, string confirmPassword, bool rememberMe);
        AccountResult Login(string username, string password, bool rememberMe);
        AccountResult TryResumeSession();
        void Logout();
        AccountResult DeleteAccount(string typedUsername);
    }
}
