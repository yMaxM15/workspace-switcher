using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    private IntPtr _hWnd = IntPtr.Zero;
    private NativeMethods.WndProc? _wndProcDelegate;
    private bool _disposed;
    private const string WindowClassName = "WorkspaceSwitcher_HotkeyWindow";

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
            success = NativeMethods.RegisterHotKey(_hWnd, id, (uint)modifiers, virtualKey);
            if (success)
            {
                _bindings[id] = binding;
            }
            waitHandle.Set();
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
            success = NativeMethods.UnregisterHotKey(_hWnd, id);
            waitHandle.Set();
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

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hWnd, NativeMethods.WM_USER, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void RunMessageLoop()
    {
        try
        {
            _wndProcDelegate = CustomWndProc;

            var wndClass = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = _wndProcDelegate,
                hInstance = NativeMethods.GetModuleHandle(null),
                lpszClassName = WindowClassName
            };

            NativeMethods.RegisterClassEx(ref wndClass);

            _hWnd = NativeMethods.CreateWindowEx(
                0,
                WindowClassName,
                "WorkspaceSwitcher_MessageWindow",
                0,
                0, 0, 0, 0,
                NativeMethods.HWND_MESSAGE,
                IntPtr.Zero,
                wndClass.hInstance,
                IntPtr.Zero
            );

            _initializedEvent.Set();

            // Win32 Message Pump
            while (!_disposed && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (_hWnd != IntPtr.Zero)
            {
                NativeMethods.DestroyWindow(_hWnd);
                _hWnd = IntPtr.Zero;
            }
            NativeMethods.UnregisterClass(WindowClassName, NativeMethods.GetModuleHandle(null));
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_USER:
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
                return IntPtr.Zero;

            case NativeMethods.WM_HOTKEY:
                int id = wParam.ToInt32();
                uint vk = (uint)(((ulong)lParam >> 16) & 0xFFFF);
                var modifiers = (KeyModifiers)((uint)lParam & 0xFFFF);

                _bindings.TryGetValue(id, out var binding);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        HotKeyPressed?.Invoke(this, new HotKeyEventArgs(id, modifiers, vk, binding));
                    }
                    catch
                    {
                        // Protect message loop from subscriber exceptions
                    }
                });
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();

        if (_hWnd != IntPtr.Zero)
        {
            NativeMethods.PostMessage(_hWnd, NativeMethods.WM_DESTROY, IntPtr.Zero, IntPtr.Zero);
        }

        _messageLoopThread.Join(TimeSpan.FromSeconds(2));
        _initializedEvent.Dispose();
        GC.SuppressFinalize(this);
    }
}
