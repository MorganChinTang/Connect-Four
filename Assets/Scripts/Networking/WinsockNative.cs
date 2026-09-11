using System;
using System.Net;
using System.Runtime.InteropServices;

public static class WinsockNative
{
    public const int AF_INET = 2;
    public const int SOCK_STREAM = 1;
    public const int IPPROTO_TCP = 6;
    public const int SOCKET_ERROR = -1;
    public static readonly IntPtr INVALID_SOCKET = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct WSAData
    {
        public short wVersion;
        public short wHighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string szDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        public string szSystemStatus;
        public short iMaxSockets;
        public short iMaxUdpDg;
        public IntPtr lpVendorInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SockAddrIn
    {
        public short sin_family;
        public ushort sin_port;
        public uint sin_addr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] sin_zero;
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern short WSAStartup(ushort wVersionRequested, out WSAData wsaData);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int WSACleanup();

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int WSAGetLastError();

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern IntPtr socket(int af, int type, int protocol);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int closesocket(IntPtr socketHandle);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int bind(IntPtr socketHandle, byte[] name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int listen(IntPtr socketHandle, int backlog);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern IntPtr accept(IntPtr socketHandle, IntPtr addr, ref int addrlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int connect(IntPtr socketHandle, byte[] name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int send(IntPtr socketHandle, byte[] buffer, int length, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int recv(IntPtr socketHandle, byte[] buffer, int length, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    public static extern int shutdown(IntPtr socketHandle, int how);

    public static byte[] CreateSockAddrAny(int port)
    {
        return CreateSockAddr("0.0.0.0", port);
    }

    public static byte[] CreateSockAddr(string ipAddress, int port)
    {
        var ip = IPAddress.Parse(ipAddress);
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4)
        {
            throw new ArgumentException("Only IPv4 addresses are supported.");
        }

        var sockaddr = new SockAddrIn
        {
            sin_family = AF_INET,
            sin_port = unchecked((ushort)IPAddress.HostToNetworkOrder((short)port)),
            sin_addr = BitConverter.ToUInt32(bytes, 0),
            sin_zero = new byte[8]
        };

        var size = Marshal.SizeOf<SockAddrIn>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(sockaddr, ptr, false);
            var output = new byte[size];
            Marshal.Copy(ptr, output, 0, size);
            return output;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
