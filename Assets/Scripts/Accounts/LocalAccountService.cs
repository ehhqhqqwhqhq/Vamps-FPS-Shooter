using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Vamp.Core;

namespace Vamp.Accounts
{
    /// <summary>
    /// OFFLINE account service: accounts live in a local database file so the whole account flow
    /// (create → validate → normalize → reserved check → uniqueness → hash → id → profile/stats/settings/social →
    /// session → enter game) works today. It mirrors what the future server does, so swapping in an online
    /// implementation of <see cref="IAccountService"/> doesn't change any UI.
    /// Security features implemented: PBKDF2 hashing, unique normalized usernames, reserved names, login/registration
    /// rate limiting, session tokens (hashed at rest) with expiry, revocation on logout, Remember Me tokens (never the
    /// password), account status checks, deletion with username confirmation.
    /// </summary>
    public sealed class LocalAccountService : IAccountService
    {
        private const string DbFile = "accounts.json";
        private const string RememberKey = "vamp.session.token";
        private const string GenericLoginError = "INCORRECT USERNAME OR PASSWORD";

        private readonly AccountRules _rules;
        private AccountsDatabase _db;
        private readonly Dictionary<string, List<float>> _failedLogins = new Dictionary<string, List<float>>();
        private readonly Dictionary<string, float> _lockedUntil = new Dictionary<string, float>();
        private readonly List<float> _registrations = new List<float>();
        private string _currentTokenHash;

        public AccountRecord Current { get; private set; }
        public bool IsLoggedIn { get { return Current != null; } }
        public bool IsOnline { get { return false; } }

        public event Action<AccountRecord> LoggedIn;
        public event Action LoggedOut;
        public event Action<AccountRecord> AccountCreated;

        public LocalAccountService()
        {
            _rules = AccountRules.Load();
            AccountsDatabase db;
            _db = JsonStore.TryLoad(DbFile, out db) ? db : new AccountsDatabase();
            if (_db.accounts == null) _db.accounts = new List<AccountRecord>();
            if (_db.sessions == null) _db.sessions = new List<SessionRecord>();
            PurgeExpiredSessions();
        }

        public string ValidateUsername(string username) { return _rules.ValidateUsername(username); }
        public string ValidatePassword(string password, string confirm) { return _rules.ValidatePassword(password, confirm); }
        public PasswordStrength EvaluateStrength(string password) { return _rules.Evaluate(password); }

        // ------------------------------------------------------------------ Create

        public AccountResult CreateAccount(string username, string password, string confirmPassword, bool rememberMe)
        {
            username = (username ?? "").Trim();

            // Registration rate limit
            float now = Time.realtimeSinceStartup;
            _registrations.RemoveAll(t => now - t > 3600f);
            if (_registrations.Count >= _rules.maxRegistrationsPerHour) return AccountResult.Fail("TOO MANY ACCOUNTS CREATED. TRY AGAIN LATER");

            string error = _rules.ValidateUsername(username);
            if (error != null) return AccountResult.Fail(error);
            string normalized = AccountRules.Normalize(username);
            if (FindByNormalized(normalized) != null) return AccountResult.Fail("USERNAME ALREADY TAKEN");
            error = _rules.ValidatePassword(password, confirmPassword);
            if (error != null) return AccountResult.Fail(error);

            var account = new AccountRecord
            {
                id = Guid.NewGuid().ToString("N"),
                username = username,
                username_normalized = normalized,
                password_hash = PasswordHasher.Hash(password),
                created_at = Now(),
                last_login = Now(),
                account_status = AccountStatus.Active
            };

            // Unique constraint (the DB would enforce this with a unique index on username_normalized).
            if (FindByNormalized(normalized) != null) return AccountResult.Fail("USERNAME ALREADY TAKEN");
            _db.accounts.Add(account);
            Persist();
            _registrations.Add(now);

            // Profile, stats, settings and social profile are created by listeners (ProgressionService etc.).
            if (AccountCreated != null) AccountCreated(account);

            StartSession(account, rememberMe);
            return AccountResult.Ok(account);
        }

        // ------------------------------------------------------------------ Login

        public AccountResult Login(string username, string password, bool rememberMe)
        {
            string normalized = AccountRules.Normalize(username);
            float now = Time.realtimeSinceStartup;

            float until;
            if (_lockedUntil.TryGetValue(normalized, out until) && now < until)
                return AccountResult.Fail("TOO MANY ATTEMPTS. TRY AGAIN IN " + Mathf.CeilToInt(until - now) + "s");

            var account = FindByNormalized(normalized);
            // Always hash, even for unknown users, so timing doesn't reveal which field was wrong.
            bool ok = PasswordHasher.Verify(password ?? "", account != null ? account.password_hash : DummyHash);
            if (account == null || !ok)
            {
                RecordFailure(normalized, now);
                return AccountResult.Fail(GenericLoginError);
            }

            _failedLogins.Remove(normalized);
            string statusError = StatusError(account);
            if (statusError != null) return AccountResult.Fail(statusError);

            account.last_login = Now();
            Persist();
            StartSession(account, rememberMe);
            return AccountResult.Ok(account);
        }

        public AccountResult TryResumeSession()
        {
            string token = PlayerPrefs.GetString(RememberKey, "");
            if (string.IsNullOrEmpty(token)) return AccountResult.Fail("NO SESSION");
            string hash = PasswordHasher.Sha256(token);
            PurgeExpiredSessions();
            var session = _db.sessions.Find(s => s.token_hash == hash && !s.revoked);
            if (session == null)
            {
                PlayerPrefs.DeleteKey(RememberKey);
                return AccountResult.Fail("SESSION EXPIRED");
            }
            var account = _db.accounts.Find(a => a.id == session.account_id);
            if (account == null || StatusError(account) != null)
            {
                session.revoked = true;
                Persist();
                PlayerPrefs.DeleteKey(RememberKey);
                return AccountResult.Fail(account == null ? "SESSION EXPIRED" : StatusError(account));
            }
            account.last_login = Now();
            Persist();
            _currentTokenHash = hash;
            Current = account;
            if (LoggedIn != null) LoggedIn(account);
            return AccountResult.Ok(account);
        }

        public void Logout()
        {
            if (!string.IsNullOrEmpty(_currentTokenHash))
            {
                var s = _db.sessions.Find(x => x.token_hash == _currentTokenHash);
                if (s != null) s.revoked = true;
                Persist();
            }
            _currentTokenHash = null;
            PlayerPrefs.DeleteKey(RememberKey);
            PlayerPrefs.Save();
            Current = null;
            if (LoggedOut != null) LoggedOut();
        }

        public AccountResult DeleteAccount(string typedUsername)
        {
            if (Current == null) return AccountResult.Fail("NOT LOGGED IN");
            if (AccountRules.Normalize(typedUsername) != Current.username_normalized)
                return AccountResult.Fail("USERNAME DOES NOT MATCH");

            var account = Current;
            account.account_status = AccountStatus.Deleted;
            account.password_hash = "";
            foreach (var s in _db.sessions) if (s.account_id == account.id) s.revoked = true;
            Persist();
            JsonStore.DeleteFolder("accounts/" + account.id);
            Logout();
            return AccountResult.Ok(account);
        }

        // ------------------------------------------------------------------ Helpers

        private static readonly string DummyHash = PasswordHasher.Hash("vamp-dummy-password");

        private void StartSession(AccountRecord account, bool rememberMe)
        {
            string token = PasswordHasher.NewToken();
            DateTime created = DateTime.UtcNow;
            DateTime expires = rememberMe ? created.AddDays(_rules.rememberMeDays) : created.AddHours(_rules.sessionHours);
            var session = new SessionRecord
            {
                account_id = account.id,
                token_hash = PasswordHasher.Sha256(token),
                created_at = created.ToString("o", CultureInfo.InvariantCulture),
                expires_at = expires.ToString("o", CultureInfo.InvariantCulture),
                remember_me = rememberMe
            };
            _db.sessions.Add(session);
            Persist();
            _currentTokenHash = session.token_hash;

            if (rememberMe) PlayerPrefs.SetString(RememberKey, token); // a revocable token - never the password
            else PlayerPrefs.DeleteKey(RememberKey);
            PlayerPrefs.Save();

            Current = account;
            if (LoggedIn != null) LoggedIn(account);
        }

        private void RecordFailure(string normalized, float now)
        {
            List<float> list;
            if (!_failedLogins.TryGetValue(normalized, out list))
            {
                list = new List<float>();
                _failedLogins[normalized] = list;
            }
            list.RemoveAll(t => now - t > _rules.loginWindowSeconds);
            list.Add(now);
            if (list.Count >= _rules.maxFailedLoginsPerUser)
            {
                _lockedUntil[normalized] = now + _rules.loginLockoutSeconds;
                list.Clear();
            }
        }

        private static string StatusError(AccountRecord a)
        {
            switch (a.account_status)
            {
                case AccountStatus.Suspended: return "THIS ACCOUNT IS SUSPENDED";
                case AccountStatus.Banned: return "THIS ACCOUNT IS BANNED";
                case AccountStatus.Deleted: return GenericLoginError;
                default: return null;
            }
        }

        private AccountRecord FindByNormalized(string normalized)
        {
            return _db.accounts.Find(a => a.username_normalized == normalized);
        }

        private void PurgeExpiredSessions()
        {
            DateTime now = DateTime.UtcNow;
            _db.sessions.RemoveAll(s =>
            {
                DateTime exp;
                bool parsed = DateTime.TryParse(s.expires_at, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out exp);
                return s.revoked || !parsed || exp < now;
            });
        }

        private void Persist()
        {
            JsonStore.Save(DbFile, _db);
        }

        private static string Now()
        {
            return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }
    }
}
