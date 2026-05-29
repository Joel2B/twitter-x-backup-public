using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public sealed class DynamicParallelOptions
{
    public int MinDegreeOfParallelism { get; set; } = 4;
    public int MaxDegreeOfParallelism { get; set; } = 32;
    public int StartDegreeOfParallelism { get; set; } = 4;

    /// <summary>Si el promedio de duraciones cae por debajo de TargetDuration, subimos; si lo excede, bajamos.</summary>
    public TimeSpan TargetDuration { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Si true, al ir "rápido" saltará directo a Max (en vez de solo doblar).</summary>
    public bool JumpToMaxOnFastAverage { get; set; } = false;

    /// <summary>Si true, una tarea > HeavyThreshold reduce inmediato a Min.</summary>
    public bool EnableHeavyCut { get; set; } = false;

    /// <summary>Umbral de "tarea pesada" (solo si EnableHeavyCut = true).</summary>
    public TimeSpan HeavyThreshold { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Si true, al reducir concurrencia no se admitirán nuevas tareas hasta que inFlight ≤ current.</summary>
    public bool StrictDecreaseGate { get; set; } = false;

    /// <summary>Habilita logs de depuración.</summary>
    public bool EnableDebug { get; set; } = false;

    /// <summary>Destino para logs. Si es null y EnableDebug=true, usa Console.WriteLine.</summary>
    public Action<string>? DebugSink { get; set; } = null;

    /// <summary>Nombre opcional para identificar la ejecución en logs.</summary>
    public string? DebugName { get; set; } = null;

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}

public static class DynamicParallel
{
    public static async Task ForEachAsync<T>(
        IEnumerable<T> source,
        DynamicParallelOptions options,
        Func<T, CancellationToken, Task> body
    )
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        if (body is null)
            throw new ArgumentNullException(nameof(body));

        var scheduler = new Scheduler(
            min: options.MinDegreeOfParallelism,
            max: options.MaxDegreeOfParallelism,
            start: options.StartDegreeOfParallelism,
            target: options.TargetDuration,
            jumpToMaxOnFastAvg: options.JumpToMaxOnFastAverage,
            enableHeavyCut: options.EnableHeavyCut,
            heavyThreshold: options.HeavyThreshold,
            strictGate: options.StrictDecreaseGate,
            enableDebug: options.EnableDebug,
            debugSink: options.DebugSink,
            debugName: options.DebugName
        );

        var ct = options.CancellationToken;
        var tasks = new List<Task>();

        foreach (var item in source)
        {
            ct.ThrowIfCancellationRequested();
            await scheduler.WaitAsync(ct);
            tasks.Add(RunItem(item, body, scheduler, ct));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task RunItem<T>(
        T item,
        Func<T, CancellationToken, Task> body,
        Scheduler scheduler,
        CancellationToken ct
    )
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await body(item, ct);
        }
        finally
        {
            sw.Stop();
            scheduler.Report(sw.Elapsed);
            scheduler.Release();
        }
    }

    private sealed class Scheduler
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lock = new();

        private int _current;
        private readonly int _min;
        private readonly int _max;
        private readonly TimeSpan _target;
        private readonly bool _jumpToMaxOnFastAvg;
        private readonly bool _enableHeavyCut;
        private readonly TimeSpan _heavyThreshold;
        private readonly bool _strictGate;

        private long _totalTicks;
        private int _samples;
        private int _sampleWindow;

        private int _banked; // permisos que se absorberán en futuros Release()
        private int _inFlight; // tareas en vuelo

        private readonly bool _dbg;
        private readonly Action<string>? _sink;
        private readonly string _name;

        public Scheduler(
            int min,
            int max,
            int start,
            TimeSpan target,
            bool jumpToMaxOnFastAvg,
            bool enableHeavyCut,
            TimeSpan heavyThreshold,
            bool strictGate,
            bool enableDebug,
            Action<string>? debugSink,
            string? debugName
        )
        {
            if (min <= 0)
                throw new ArgumentOutOfRangeException(nameof(min));
            if (max < min)
                throw new ArgumentOutOfRangeException(nameof(max));
            if (start < min || start > max)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (target <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(target));
            if (heavyThreshold <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(heavyThreshold));

            _min = min;
            _max = max;
            _current = start;
            _target = target;
            _jumpToMaxOnFastAvg = jumpToMaxOnFastAvg;
            _enableHeavyCut = enableHeavyCut;
            _heavyThreshold = heavyThreshold;
            _strictGate = strictGate;

            _sampleWindow = Math.Max(1, _current / 2);
            _semaphore = new SemaphoreSlim(start, max);
            _banked = 0;
            _inFlight = 0;

            _dbg = enableDebug;
            _sink = debugSink ?? (enableDebug ? (s => Console.WriteLine(s)) : null);
            _name = string.IsNullOrWhiteSpace(debugName) ? "DynamicParallel" : debugName!;
            Debug(
                $"INIT current={_current} min={_min} max={_max} win={_sampleWindow} banked={_banked} inFlight={_inFlight}"
            );
            DebugState("INIT");
        }

        public async Task WaitAsync(CancellationToken ct)
        {
            if (_strictGate)
            {
                while (Volatile.Read(ref _inFlight) >= Volatile.Read(ref _current))
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(25, ct);
                }
            }

            await _semaphore.WaitAsync(ct);
            Interlocked.Increment(ref _inFlight);
            DebugIf($"WAIT OK -> inFlight={_inFlight}");
            DebugState("WAIT");
        }

        public void Release()
        {
            bool released = false;

            lock (_lock)
            {
                if (_banked > 0)
                {
                    _banked--;
                    var inF = Interlocked.Decrement(ref _inFlight);

                    // FAILSAFE: si ya no queda nadie corriendo y el sem quedó en 0, suelta 1 permiso
                    if (inF == 0 && _semaphore.CurrentCount == 0 && _current > 0)
                    {
                        _semaphore.Release(1);
                        released = true;
                    }

                    Debug(
                        $"RELEASE uses banked -> banked={_banked} inFlight={inF} (failsafe={released})"
                    );
                    DebugState("RELEASE-BANK");
                    return;
                }

                if (_semaphore.CurrentCount >= _max)
                {
                    _banked++;
                    var inF2 = Interlocked.Decrement(ref _inFlight);
                    Debug($"RELEASE banked (at max) -> banked={_banked} inFlight={inF2}");
                    DebugState("RELEASE-ATMAX");
                    return;
                }
            }

            _semaphore.Release();
            var inFlightNow = Interlocked.Decrement(ref _inFlight);
            DebugIf($"RELEASE -> inFlight={inFlightNow}");
            DebugState("RELEASE");
        }

        public void Report(TimeSpan duration)
        {
            lock (_lock)
            {
                if (_enableHeavyCut && duration > _heavyThreshold && _current > _min)
                {
                    Debug(
                        $"HEAVY CUT duration={duration.TotalMilliseconds:F1}ms > {_heavyThreshold.TotalMilliseconds:F1}ms -> DOWN to min={_min}"
                    );
                    SetCurrentDown(_min, "HEAVY_CUT");
                    return;
                }

                _totalTicks += duration.Ticks;
                _samples++;

                if (_samples < _sampleWindow)
                    return;

                var avg = TimeSpan.FromTicks(_totalTicks / _samples);
                Debug(
                    $"WINDOW avg={avg.TotalMilliseconds:F1}ms target={_target.TotalMilliseconds:F1}ms samples={_samples} win={_sampleWindow} current={_current} banked={_banked} inFlight={_inFlight}"
                );
                _totalTicks = 0;
                _samples = 0;

                if (avg < _target)
                {
                    var desired = _jumpToMaxOnFastAvg ? _max : Math.Min(_max, _current * 2);
                    if (desired > _current)
                        SetCurrentUp(desired, "FAST_AVG");
                }
                else if (avg > _target)
                {
                    var desired = Math.Max(_min, _current / 2);
                    if (desired < _current)
                        SetCurrentDown(desired, "SLOW_AVG");
                }
            }
        }

        private void SetCurrentUp(int desired, string reason = "UP")
        {
            int delta = desired - _current;
            if (delta <= 0)
                return;

            int headroom = Math.Max(0, _max - _semaphore.CurrentCount);
            if (headroom == 0)
            {
                _current = desired;
                _sampleWindow = Math.Max(1, _current / 2);
                Debug(
                    $"{reason} NO-RELEASE headroom=0 -> current={_current} win={_sampleWindow} banked={_banked} inFlight={_inFlight}"
                );
                DebugState($"{reason}-NOREL");
                return;
            }

            int fromBank = Math.Min(_banked, Math.Min(delta, headroom));
            if (fromBank > 0)
            {
                _semaphore.Release(fromBank);
                _banked -= fromBank;
                headroom -= fromBank;
                Debug($"{reason} (BANK RETURN) -> +{fromBank}   banked={_banked}");
            }

            int remainingDelta = Math.Max(0, delta - fromBank);
            if (remainingDelta > 0 && headroom > 0)
            {
                int toRelease = Math.Min(remainingDelta, headroom);
                _semaphore.Release(toRelease);
                Debug($"{reason} (RELEASE)     -> +{toRelease}");
            }

            _current = desired;
            _sampleWindow = Math.Max(1, _current / 2);
            Debug(
                $"{reason} DONE current={_current} win={_sampleWindow} banked={_banked} inFlight={_inFlight}"
            );
            DebugState($"{reason}-DONE");
        }

        private void SetCurrentDown(int desired, string reason = "DOWN")
        {
            if (desired >= _current)
                return;

            int sem = _semaphore.CurrentCount;
            int inF = Volatile.Read(ref _inFlight);

            int effectiveAfterBank = (sem + inF) - _banked;
            int needRemove = Math.Max(0, effectiveAfterBank - desired);

            _current = desired;
            _sampleWindow = Math.Max(1, _current / 2);

            int drained = 0;
            for (int i = 0; i < needRemove; i++)
            {
                if (_semaphore.Wait(0))
                    drained++;
                else
                    break;
            }

            int pendingAbsorb = needRemove - drained;
            _banked += pendingAbsorb;

            Debug(
                $"{reason} current={_current} win={_sampleWindow} drained={drained} pendingAbsorb={pendingAbsorb} banked={_banked} inFlight={_inFlight}"
            );
            DebugState($"{reason}");
        }

        private void Debug(string msg)
        {
            if (!_dbg)
                return;
            _sink?.Invoke($"[{_name}] {DateTimeOffset.Now:HH:mm:ss.fff} {msg}");
        }

        private void DebugIf(string msg)
        {
            if (_dbg)
                _sink?.Invoke($"[{_name}] {DateTimeOffset.Now:HH:mm:ss.fff} {msg}");
        }

        private void DebugState(string tag)
        {
            if (!_dbg)
                return;
            int sem = _semaphore.CurrentCount;
            Debug(
                $"{tag} STATE sem={sem} inFlight={_inFlight} banked={_banked} current={_current}"
            );
        }
    }
}
