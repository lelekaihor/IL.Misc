﻿using System.Collections.Concurrent;

namespace IL.Misc.Concurrency;

public static class LockManager
{
    private static readonly ConcurrentDictionary<string, Lazy<Lock>> Locks = new();

    public static IDisposable GetLock(string key, int initialConcurrentCalls = 1, int maxConcurrentCalls = 1)
    {
        var lazyLock = Locks.GetOrAdd(key, new Lazy<Lock>(() => new Lock(initialConcurrentCalls, maxConcurrentCalls)));
        var concurrentLock = lazyLock.Value;
        concurrentLock.Wait();
        return concurrentLock;
    }

    public static async Task<IDisposable> GetLockAsync(string key, int initialConcurrentCalls = 1, int maxConcurrentCalls = 1)
    {
        var lazyLock = Locks.GetOrAdd(key, new Lazy<Lock>(() => new Lock(initialConcurrentCalls, maxConcurrentCalls)));
        var concurrentLock = lazyLock.Value;
        await concurrentLock.WaitAsync();
        return concurrentLock;
    }

    public class Lock : IDisposable
    {
        private readonly SemaphoreSlim _semaphoreSlim;

        public Lock(int initialConcurrentCalls = 1, int maxConcurrentCalls = 1)
        {
            _semaphoreSlim = new SemaphoreSlim(initialConcurrentCalls, maxConcurrentCalls);
        }

        public void Wait()
        {
            _semaphoreSlim.Wait();
        }

        public async Task WaitAsync()
        {
            await _semaphoreSlim.WaitAsync();
        }

        public void Dispose()
        {
            _semaphoreSlim.Release();
        }

        public int GetState()
        {
            return _semaphoreSlim.CurrentCount;
        }
    }
}