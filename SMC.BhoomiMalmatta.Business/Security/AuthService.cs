using System;
using System.Collections.Generic;
using SMC.BhoomiMalmatta.Business.Abstractions;
using SMC.BhoomiMalmatta.Models.Common;
using SMC.BhoomiMalmatta.Models.Constants;
using SMC.BhoomiMalmatta.Models.Dto;

namespace SMC.BhoomiMalmatta.Business.Security
{
    /// <summary>Failed-login throttle per username+IP (in-memory; resets on application restart).</summary>
    public sealed class LoginThrottle
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<DateTime>> _failures = new Dictionary<string, List<DateTime>>();
        private readonly int _max; private readonly TimeSpan _window;
        public LoginThrottle(int maxFailures = 5, int windowMinutes = 15) { _max = maxFailures; _window = TimeSpan.FromMinutes(windowMinutes); }

        public bool IsLocked(string key, DateTime now)
        {
            lock (_gate)
            {
                List<DateTime> list;
                if (!_failures.TryGetValue(key, out list)) return false;
                list.RemoveAll(t => now - t > _window);
                return list.Count >= _max;
            }
        }
        public void Fail(string key, DateTime now)
        {
            lock (_gate)
            {
                List<DateTime> list;
                if (!_failures.TryGetValue(key, out list)) { list = new List<DateTime>(); _failures[key] = list; }
                list.Add(now);
            }
        }
        public void Reset(string key) { lock (_gate) { _failures.Remove(key); } }
    }

    public sealed class AuthService
    {
        private readonly IUserStore _users; private readonly IPasswordVerifier _verifier; private readonly IAuditWriter _audit; private readonly IClock _clock; private readonly LoginThrottle _throttle;
        public AuthService(IUserStore users, IPasswordVerifier verifier, IAuditWriter audit, IClock clock, LoginThrottle throttle)
        { _users = users; _verifier = verifier; _audit = audit; _clock = clock; _throttle = throttle; }

        private static readonly Error Invalid = new Error("LOGIN_INVALID", "वापरकर्ता नाव किंवा पासवर्ड चुकीचा आहे.");

        public Result<AuthenticatedUser> Login(string username, string password, string ip)
        {
            username = (username ?? string.Empty).Trim();
            var now = _clock.UtcNow;
            var key = username.ToLowerInvariant() + "|" + (ip ?? "");
            if (username.Length == 0 || string.IsNullOrEmpty(password)) return Result.Failure<AuthenticatedUser>(Invalid);
            if (_throttle.IsLocked(key, now))
                return Result.Failure<AuthenticatedUser>(new Error("LOGIN_LOCKED", "अनेक चुकीचे प्रयत्न झाले. कृपया १५ मिनिटांनी पुन्हा प्रयत्न करा."));

            var u = _users.FindByUsername(username);
            if (u == null) { _throttle.Fail(key, now); return Result.Failure<AuthenticatedUser>(Invalid); }

            var ok = u.IsActive && Array.IndexOf(Roles.OfficerRoles, u.Role) >= 0 && _verifier.Verify(password, u.PasswordHash);
            if (!ok)
            {
                _throttle.Fail(key, now);
                Audit(u, AuditActions.LoginFailed, ip, now);
                return Result.Failure<AuthenticatedUser>(Invalid);
            }
            _throttle.Reset(key);
            _users.RecordLogin(u.Id, now);
            Audit(u, AuditActions.LoginSuccess, ip, now);
            return Result.Success(new AuthenticatedUser { Id = u.Id, Username = u.Username, FullName = u.FullName, Role = u.Role, ZoneNumber = u.ZoneNumber });
        }

        public void Logout(AuthenticatedUser user, string ip)
        {
            try { _audit.Write(new AuditEntry { Action = AuditActions.Logout, EntityName = "User", EntityId = user.Id, UserId = user.Id, UserName = user.Username, IpAddress = ip, TimestampUtc = _clock.UtcNow }); }
            catch (Exception) { }
        }

        private void Audit(UserRecord u, string action, string ip, DateTime now)
        {
            try { _audit.Write(new AuditEntry { Action = action, EntityName = "User", EntityId = u.Id, UserId = u.Id, UserName = u.Username, IpAddress = ip, TimestampUtc = now }); }
            catch (Exception) { /* auditing must not block sign-in feedback */ }
        }
    }
}
