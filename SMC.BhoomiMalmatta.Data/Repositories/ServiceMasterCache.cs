using System;
using System.Runtime.Caching;
namespace SMC.BhoomiMalmatta.Data.Repositories
{
    public static class ServiceMasterCache
    {
        private static readonly object Gate = new object();
        public static T GetOrLoad<T>(string key, Func<T> loader)
        {
            var cache = MemoryCache.Default;
            var value = cache.Get(key);
            if (value != null) return (T)value;
            lock (Gate)
            {
                value = cache.Get(key);
                if (value != null) return (T)value;
                var loaded = loader();
                cache.Set(key, loaded, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(5) });
                return loaded;
            }
        }
    }
}