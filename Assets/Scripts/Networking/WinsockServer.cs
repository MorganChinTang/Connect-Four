using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

public class WinsockServer
{
    private readonly ConcurrentQueue<NetEvent> _events = new ConcurrentQueue<NetEvent>();
    private readonly object _sendLock = new object();
    private Thread _acceptThread;
    private Thread _receiveThread;
    private IntPtr _listenSocket = WinsockNative.INVALID_SOCKET;
    private IntPtr _clientSocket = WinsockNative.INVALID_SOCKET;
    private bool _wsaStarted;
    private volatile bool _running;
    private volatile bool _clientConnected;
    private readonly StringBuilder _receiveAccumulator = new StringBuilder();

    public bool IsRunning => _running;
    public bool IsClientConnected => _clientConnected;

    public bool Start(int port, out string error)
    {
        error = string.Empty;
        if (_running)
        {
            error = "Server already running.";
            return false;
        }

        if (!StartWinsock(out error))
        {
            return false;
        }

        _listenSocket = WinsockNative.socket(WinsockNative.AF_INET, WinsockNative.SOCK_STREAM, WinsockNative.IPPROTO_TCP);
        if (_listenSocket == WinsockNative.INVALID_SOCKET)
        {
            error = $"socket failed: {WinsockNative.WSAGetLastError()}";
            Stop();
            return false;
        }

        try
        {
            var addr = WinsockNative.CreateSockAddrAny(port);
            if (WinsockNative.bind(_listenSocket, addr, addr.Length) == WinsockNative.SOCKET_ERROR)
            {
                error = $"bind failed: {WinsockNative.WSAGetLastError()}";
                Stop();
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"bind preparation failed: {ex.Message}";
            Stop();
            return false;
        }

        if (WinsockNative.listen(_listenSocket, 1) == WinsockNative.SOCKET_ERROR)
        {
            error = $"listen failed: {WinsockNative.WSAGetLastError()}";
            Stop();
            return false;
        }

        _running = true;
        _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
        _acceptThread.Start();
        return true;
    }

    public bool SendLine(string line, out string error)
    {
        error = string.Empty;
        if (!_running || !_clientConnected || _clientSocket == WinsockNative.INVALID_SOCKET)
        {
            error = "No connected client.";
            return false;
        }

        var payload = Encoding.UTF8.GetBytes($"{line}\n");
        lock (_sendLock)
        {
            var totalSent = 0;
            while (totalSent < payload.Length)
            {
                var chunk = new byte[payload.Length - totalSent];
                Buffer.BlockCopy(payload, totalSent, chunk, 0, chunk.Length);
                var sent = WinsockNative.send(_clientSocket, chunk, chunk.Length, 0);
                if (sent <= 0)
                {
                    error = $"send failed: {WinsockNative.WSAGetLastError()}";
                    return false;
                }

                totalSent += sent;
            }
        }

        return true;
    }

    public bool TryDequeueEvent(out NetEvent netEvent)
    {
        return _events.TryDequeue(out netEvent);
    }

    public void Stop()
    {
        _running = false;
        _clientConnected = false;

        CloseSocket(ref _clientSocket);
        CloseSocket(ref _listenSocket);

        if (_acceptThread != null && _acceptThread.IsAlive)
        {
            _acceptThread.Join(150);
        }

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(150);
        }

        _acceptThread = null;
        _receiveThread = null;
        _receiveAccumulator.Clear();

        if (_wsaStarted)
        {
            WinsockNative.WSACleanup();
            _wsaStarted = false;
        }
    }

    private bool StartWinsock(out string error)
    {
        error = string.Empty;
        var result = WinsockNative.WSAStartup(0x0202, out _);
        if (result != 0)
        {
            error = $"WSAStartup failed: {result}";
            return false;
        }

        _wsaStarted = true;
        return true;
    }

    private void AcceptLoop()
    {
        var accepted = WinsockNative.accept(_listenSocket, IntPtr.Zero, IntPtr.Zero);
        if (!_running)
        {
            return;
        }

        if (accepted == WinsockNative.INVALID_SOCKET)
        {
            _events.Enqueue(new NetEvent(NetEventType.Error, $"accept failed: {WinsockNative.WSAGetLastError()}"));
            return;
        }

        _clientSocket = accepted;
        _clientConnected = true;
        _events.Enqueue(new NetEvent(NetEventType.Connected, string.Empty));
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        _receiveThread.Start();
    }

    private void ReceiveLoop()
    {
        var buffer = new byte[4096];
        while (_running && _clientSocket != WinsockNative.INVALID_SOCKET)
        {
            var received = WinsockNative.recv(_clientSocket, buffer, buffer.Length, 0);
            if (received > 0)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, received);
                _receiveAccumulator.Append(text);
                DrainLines();
                continue;
            }

            if (received == 0)
            {
                break;
            }

            if (_running)
            {
                _events.Enqueue(new NetEvent(NetEventType.Error, $"recv failed: {WinsockNative.WSAGetLastError()}"));
            }

            break;
        }

        _clientConnected = false;
        CloseSocket(ref _clientSocket);
        if (_running)
        {
            _events.Enqueue(new NetEvent(NetEventType.Disconnected, string.Empty));
        }
    }

    private void DrainLines()
    {
        while (true)
        {
            var current = _receiveAccumulator.ToString();
            var newline = current.IndexOf('\n');
            if (newline < 0)
            {
                return;
            }

            var line = current.Substring(0, newline).Trim('\r');
            _receiveAccumulator.Remove(0, newline + 1);
            if (!string.IsNullOrWhiteSpace(line))
            {
                _events.Enqueue(new NetEvent(NetEventType.Message, line));
            }
        }
    }

    private static void CloseSocket(ref IntPtr socketHandle)
    {
        if (socketHandle == WinsockNative.INVALID_SOCKET)
        {
            return;
        }

        WinsockNative.shutdown(socketHandle, 2);
        WinsockNative.closesocket(socketHandle);
        socketHandle = WinsockNative.INVALID_SOCKET;
    }
}
