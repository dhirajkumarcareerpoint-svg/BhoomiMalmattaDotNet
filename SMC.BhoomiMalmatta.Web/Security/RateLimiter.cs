using System;
using System.Collections.Generic;

namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Small in-memory sliding-window limiter for public endpoints (per IP / key). Resets on application restart.</summary>
    public sealed class RateLimiter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, Queue<DateTime>> _hits = new Dictionary<string, Queue<DateTime>>();
        private DateTime _lastSweep = DateTime.UtcNow;

        public bool Allow(string key, int max, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            lock (_gate)
            {
                if (now - _lastSweep > TimeSpan.FromMinutes(10)) { Sweep(now, window); _lastSweep = now; }
                Queue<DateTime> q;
                if (!_hits.TryGetValue(key, out q)) { q = new Queue<DateTime>(); _hits[key] = q; }
                while (q.Count > 0 && now - q.Peek() > window) q.Dequeue();
                if (q.Count >= max) return false;
                q.Enqueue(now); return true;
            }
        }

        private void Sweep(DateTime now, TimeSpan window)
        {
            var dead = new List<string>();
            foreach (var kv in _hits) { while (kv.Value.Count > 0 && now - kv.Value.Peek() > window) kv.Value.Dequeue(); if (kv.Value.Count == 0) dead.Add(kv.Key); }
            foreach (var k in dead) _hits.Remove(k);
        }
    }
}
