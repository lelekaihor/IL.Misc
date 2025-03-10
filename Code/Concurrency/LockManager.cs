﻿using System.Collections.Concurrent;

namespace IL.Misc.Concurrency;

public static class LockManager
{
    private const int SelfDeletionDelayInMinutes = 1;
    private static readonly ConcurrentDictionary<string, Lazy<Lock>> Locks = new();

    /// <summary>
    /// By availability, it means Lock has any available slots on semaphore or not created at all.
    /// Locks with different concurrency level with be created as different locks
    /// </summary>
    /// <param name="key"></param>
    /// <param name="maxConcurrentCalls"></param>
    /// <returns></returns>
    public static bool IsLockAvailable(string key, int maxConcurrentCalls = 1)
    {
        var lockExists = Locks.TryGetValue($"{key}{maxConcurrentCalls}", out var concurrentLock);
        return !lockExists || concurrentLock?.Value.GetState() > 0;
    }

    public static IDisposable GetLock(string key, int maxConcurrentCalls = 1, CancellationToken cancellationToken = default)
    {
        var concurrentLock = AcquireLock(key, maxConcurrentCalls);
        concurrentLock.Wait(cancellationToken);
        return concurrentLock;
    }

    public static async Task<IDisposable> GetLockAsync(string key, int maxConcurrentCalls = 1, CancellationToken cancellationToken = default)
    {
        var concurrentLock = AcquireLock(key, maxConcurrentCalls);
        await concurrentLock.WaitAsync(cancellationToken);
        return concurrentLock;
    }

    private static Lock AcquireLock(string key, int maxConcurrentCalls)
    {
        return Locks
            .GetOrAdd($"{key}{maxConcurrentCalls}",
                new Lazy<Lock>(() => new Lock(maxConcurrentCalls, () => { Locks.TryRemove(key, out _); }), LazyThreadSafetyMode.ExecutionAndPublication)
            )
            .Value;
    }

    internal sealed class Lock : IDisposable
    {
        private readonly SemaphoreSlim _semaphoreSlim;
        private readonly Action? _selfDeletionAction;
        private readonly int _maxConcurrentCalls;
        private bool _scheduledForDeletion;

        internal Lock(int maxConcurrentCalls = 1, Action? selfDeletionAction = null)
        {
            _semaphoreSlim = new SemaphoreSlim(maxConcurrentCalls, maxConcurrentCalls);
            _selfDeletionAction = selfDeletionAction;
            _maxConcurrentCalls = maxConcurrentCalls;
        }

        internal void Wait(CancellationToken cancellationToken)
        {
            _scheduledForDeletion = false;
            _semaphoreSlim.Wait(cancellationToken);
        }

        internal async Task WaitAsync(CancellationToken cancellationToken)
        {
            _scheduledForDeletion = false;
            await _semaphoreSlim.WaitAsync(cancellationToken);
        }

        public void Dispose()
        {
            _semaphoreSlim.Release();
            if (!ReadyForDeletion() || _scheduledForDeletion)
            {
                return;
            }

            Task
                .Delay(TimeSpan.FromMinutes(SelfDeletionDelayInMinutes))
                .ContinueWith(_ =>
                {
                    if (_scheduledForDeletion && ReadyForDeletion())
                    {
                        _selfDeletionAction?.Invoke();
                    }
                });
            _scheduledForDeletion = true;
        }

        private bool ReadyForDeletion()
        {
            return GetState() == _maxConcurrentCalls;
        }

        internal int GetState() => _semaphoreSlim.CurrentCount;
    }
}