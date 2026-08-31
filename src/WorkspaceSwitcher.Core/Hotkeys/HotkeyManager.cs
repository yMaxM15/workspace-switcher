using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using WorkspaceSwitcher.Core.Native;

namespace WorkspaceSwitcher.Core.Hotkeys;

public class HotkeyManager : IDisposable
{
    private static int _nextId = 9000;

    private readonly Thread _messageLoopThread;
    private readonly ManualResetEventSlim _initializedEvent = new(false);
    private readonly ConcurrentDictionary<int, HotKeyBinding> _bindings = new();
    private readonly ConcurrentQueue<Action> _workQueue = new();

    private uint _threadId;
    private bool _disposed;

    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public IReadOnlyDictionary<int, HotKeyBinding> Bindings => _bindings;

    public HotkeyManager()
    {
        _messageLoopThread = new Thread(RunMessageLoop)
        {
            Name = "WorkspaceSwitcher_HotkeyThread",
            IsBackground = true
        };
        _messageLoopThread.SetApartmentState(ApartmentState.STA);
        _messageLoopThread.Start();

        _initializedEvent.Wait(TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// Registers a hotkey binding associated with a profile name.
    /// Thread-safe: Can be called from any thread.
    /// </summary>
    public int Register(KeyModifiers modifiers, uint virtualKey, string profileName, HotKeyAction action = HotKeyAction.RestoreProfile)
    {
        int id = Interlocked.Increment(ref _nextId);

        var binding = new HotKeyBinding
        {
            Id = id,
            TargetProfileName = profileName,
            Modifiers = modifiers,
            VirtualKey = virtualKey,
            Action = action
        };

        var waitHandle = new ManualResetEventSlim(false);
        bool success = false;

        EnqueueWork(() =>
        {
            try
            {
                success = NativeMethods.RegisterHotKey(IntPtr.Zero, id, (uint)modifiers, virtualKey);
                if (success)
                {
                    _bindings[id] = binding;
                }
            }
            finally
            {
                waitHandle.Set();
            }
        });

        waitHandle.Wait(TimeSpan.FromSeconds(2));

        if (!success)
        {
            throw new InvalidOperationException($"Failed to register hotkey {modifiers}+{virtualKey}. It may be in use by another application.");
        }

        return id;
    }

    /// <summary>
    /// Unregisters an existing hotkey by its ID.
    /// </summary>
    public bool Unregister(int id)
    {
        if (!_bindings.TryRemove(id, out _))
            return false;

        var waitHandle = new ManualResetEventSlim(false);
        bool success = false;

        EnqueueWork(() =>
        {
            try
            {
                success = NativeMethods.UnregisterHotKey(IntPtr.Zero, id);
            }
            finally
            {
                waitHandle.Set();
            }
        });

        waitHandle.Wait(TimeSpan.FromSeconds(2));
        return success;
    }

    /// <summary>
    /// Unregisters all currently registered hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        var ids = new List<int>(_bindings.Keys);
        foreach (var id in ids)
        {
            Unregister(id);
        }
    }

    private void EnqueueWork(Action action)
    {
        if (_disposed) return;
        _workQueue.Enqueue(action);

        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_USER, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();

        // Force creation of the message queue for this thread
        NativeMethods.MSG dummyMsg;
        NativeMethods.PeekMessage(out dummyMsg, IntPtr.Zero, 0, 0, 0);

        _initializedEvent.Set();

        // Win32 Thread Message Loop
        while (!_disposed && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == NativeMethods.WM_USER)
            {
                while (_workQueue.TryDequeue(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // Ignore work queue errors
                    }
                }
            }
            else if (msg.message == NativeMethods.WM_HOTKEY)
            {
                int id = msg.wParam.ToInt32();
                uint vk = (uint)(((ulong)msg.lParam >> 16) & 0xFFFF);
                var modifiers = (KeyModifiers)((uint)msg.lParam & 0xFFFF);

                _bindings.TryGetValue(id, out var binding);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HotKeyPressed?.Invoke(this, new HotKeyEventArgs(id, modifiers, vk, binding));
                    }
                    catch
                    {
                        // Protect from subscriber exceptions
                    }
                });
            }
            else
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        if (_threadId != 0)
        {
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _messageLoopThread.Join(TimeSpan.FromSeconds(2));
        _initializedEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
