﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Pty.NET;

public class PtyTerminalHost<TConection> 
                                    :
                                        IAsyncDisposable
                                        , IDisposable
{
    private const uint _ctrlCExitCode = 0xC000013A;

    public PtyOptions? Options { get; init; }

    public int ProcessId
    {
        get
        {
            return
                _terminal is not null
                ?
                _terminal.Pid
                :
                -1
                ;
        }
    }


    public readonly TConection Conection;

    private readonly string _consoleHost =
                                    RuntimeInformation
                                                .IsOSPlatform
                                                        (OSPlatform.Windows)
                                    ?
                                    Path.Combine(Environment.SystemDirectory, "cmd.exe")
                                    :
                                    "sh";

    private readonly CancellationTokenSource OnOutputCancellationTokenSource;

    private IPtyConnection? _terminal;

    private readonly TaskCompletionSource<uint> _processExitedTaskCompletionSource;

    private readonly CancellationTokenSource _readingCancellationTokenSource;

    public EventHandler<PtyExitedEventArgs>? OnProcessExited { get; set; }

    public Func<PtyTerminalHost<TConection>, string, Exception, Task<bool>>? OnCaughtExceptionProcessAsync { get; set; }

    private bool _isCanceledReading;

    private bool _isExited;

    public PtyTerminalHost(TConection conection)
    {
        Conection = conection;
        _processExitedTaskCompletionSource = new TaskCompletionSource<uint>();
        OnOutputCancellationTokenSource = new CancellationTokenSource();
        _readingCancellationTokenSource = new CancellationTokenSource(100);
    }

    public async Task InputAsync(ArraySegment<byte> bytes, CancellationToken cancellationToken = default)
    {
        var buffer = bytes.ToArray()!;

        // skip start '/r', reserve end '/n'
        buffer = buffer.Skip(1).ToArray();

        await _terminal!
                    .WriterStream
                    .WriteAsync
                            (
                                buffer
                                , 0
                                , buffer.Length
                                , cancellationToken
                            );
        await _terminal!
                    .WriterStream
                    .FlushAsync
                            (cancellationToken);

    }

    public void Resize(int columns, int rows)
    {
        if (_terminal is not null)
        {
            _terminal.Resize(columns, rows);
        }
    }

    public void Kill()
    {
        if (_terminal is not null)
        {
            _terminal!.Kill();
        }
    }

    public void Kill(int milliseconds)
    {
        if (_terminal is not null)
        {
           _terminal!.WaitForExit(milliseconds);
        }
    }

    public async Task<bool> StartRunAsync
                                (
                                    Func
                                        <
                                            PtyTerminalHost<TConection>
                                            , ArraySegment<byte>
                                            , Task
                                        >
                                            onTerminalOutputProcessAsync
                                    , int bufferBytesLength = 8 * 1024
                                )
    {
        if (_terminal is null)
        {
            Options!.App = _consoleHost;
            _terminal = await PtyProvider
                                    .SpawnAsync
                                            (
                                                Options!
                                                , OnOutputCancellationTokenSource.Token
                                            );
            _terminal.ProcessExited += (sender, e) =>
            {
                _processExitedTaskCompletionSource.TrySetResult((uint) _terminal.ExitCode);
                OnProcessExited?.Invoke(this, e);
            };
        }
        string output = string.Empty;
 
        var bytes = new byte[bufferBytesLength];
        var listeningOutputCancellationToken =
                    OnOutputCancellationTokenSource.Token;
        while
            (
                !listeningOutputCancellationToken
                                .IsCancellationRequested
                &&
                !_processExitedTaskCompletionSource
                                .Task
                                .IsCompleted
            )
        {
            int r = 0;
            do
            {
                try
                {
                    r = await _terminal
                                    .ReaderStream
                                    .ReadAsync
                                            (
                                                bytes
                                                , 0
                                                , bytes.Length
                                                , _readingCancellationTokenSource.Token
                                            );
                }
                catch (IOException exception)
                {
                    var context = $@"On ""{nameof(StartRunAsync)}"" processing, Caught Exception Type: ""{exception.GetType().Name}"" @ {DateTime.Now}";

                    if (OnCaughtExceptionProcessAsync is not null)
                    {
                        var needRethrow = await OnCaughtExceptionProcessAsync(this, context, exception);
                        if (needRethrow)
                        {
                            throw;
                        }
                    }

                    var message = $"On {nameof(context)}: {context}\r\nCaught Exception:\r\n{exception}";
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    var messageArraySegment = new ArraySegment<byte>(messageBytes, 0, messageBytes.Length);

                    if (Conection != null)
                    {
                        await
                            onTerminalOutputProcessAsync
                                                (
                                                    this
                                                    , messageArraySegment
                                                );
                    }
                    _readingCancellationTokenSource.Cancel();
                    _isCanceledReading = true;
                }
                if 
                    (
                        _isCanceledReading
                        &&
                        _readingCancellationTokenSource
                                            .IsCancellationRequested
                    )
                {
                    break;
                }
                var reseted = false;
                while (!(reseted = _readingCancellationTokenSource.TryReset()));
            }
            while (r <= 0);

            if (r > 0)
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(bytes, 0, r);

                if (Conection != null)
                {
                    await
                        onTerminalOutputProcessAsync
                                            (
                                                this
                                                , buffer
                                            );
                }
            }
            if (_isCanceledReading)
            {
                break;
            }
        }
        return true;            
    }

    public async Task<bool> ExitAsync()
    {
        bool r;
        while
            (
                !(r = await ExitOnceAsync())
            );
        return r;
    
    }

    public async Task<bool> ExitOnceAsync()
    {
        if (_isExited)
        {
            return
                _isExited;
        }

        var r = false;

        _readingCancellationTokenSource.Cancel();
        _isCanceledReading = true;

        var timeoutToken = OnOutputCancellationTokenSource.Token;
        using
            (
                timeoutToken
                            .Register
                                    (
                                        () =>
                                        {
                                            _processExitedTaskCompletionSource.TrySetCanceled(timeoutToken);
                                        }
                                    )
            )
        {
            _terminal!.Dispose();
            uint exitCode = await _processExitedTaskCompletionSource.Task;
            r =
                (
                    exitCode == _ctrlCExitCode      // WinPty terminal exit code.
                    ||
                    exitCode == 1                   // Pseudo Console exit code on Win 10.
                    ||
                    exitCode == 0                   // pty exit code on *nix.
                );
        }
        _isExited = r;
        _terminal!.WaitForExit(1000 * 10);
        return r;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    public void Dispose()
    {
        _ = !ExitAsync().Result;
        _terminal!.Dispose();
    }
}