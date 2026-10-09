using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Data.Repositories
{
    public sealed class UserStore : IUserStore
    {
        public UserRecord FindByUsername(string username)
        {
            using (var db = new SmcDbContext())
            {
                var u = db.Users.AsNoTracking().FirstOrDefault(x => x.Username == username && !x.IsDeleted);
                return u == null ? null : new UserRecord { Id = u.Id, Username = u.Username, PasswordHash = u.PasswordHash, FullName = u.FullName, Role = u.Role, IsActive = u.IsActive, ZoneNumber = u.ZoneNumber, Mobile = u.Mobile };
            }
        }

        /// <summary>Updates only LastLoginAt (the column exists for this purpose).</summary>
        public void RecordLogin(int userId, DateTime utc)
        {
            using (var db = new SmcDbContext())
                db.Database.ExecuteSqlCommand("UPDATE dbo.MR_AST_Users SET LastLoginAt = @p0 WHERE Id = @p1", utc, userId);
        }

        public List<string> GetActiveMobilesByRole(string role)
        {
            using (var db = new SmcDbContext())
                return db.Users.AsNoTracking().Where(x => x.Role == role && x.IsActive && !x.IsDeleted && x.Mobile != null).Select(x => x.Mobile).ToList();
        }
    }
}
