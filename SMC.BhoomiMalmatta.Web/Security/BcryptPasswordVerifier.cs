using System;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Verifies the existing bcrypt hashes in MR_AST_Users.PasswordHash ($2a$/$2b$/$2y$). Plain-text passwords are never stored or compared.</summary>
    public sealed class BcryptPasswordVerifier : IPasswordVerifier
    {
        public bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;
            try { return BCrypt.Net.BCrypt.Verify(password, storedHash); }
            catch (Exception) { return false; } // malformed hash / unsupported prefix => not verified
        }
    }
}
