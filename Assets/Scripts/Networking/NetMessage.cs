using System;

public enum NetEventType
{
    Connected,
    Disconnected,
    Message,
    Error
}

public struct NetEvent
{
    public NetEventType Type;
    public string Payload;

    public NetEvent(NetEventType type, string payload)
    {
        Type = type;
        Payload = payload;
    }
}

public enum NetMessageType
{
    Unknown,
    Hello,
    Start,
    Move,
    State,
    RematchRequest,
    RematchAccept,
    Quit,
    Error
}

public static class NetMessage
{
    public const char Separator = '|';

    public static string Hello(string playerName) => $"HELLO{Separator}{playerName}";
    public static string Start(int playerId) => $"START{Separator}{playerId}";
    public static string Move(int column) => $"MOVE{Separator}{column}";
    public static string State(string boardPayload, int currentTurn, int result) => $"STATE{Separator}{boardPayload}{Separator}{currentTurn}{Separator}{result}";
    public static string RematchRequest() => "REMATCH_REQUEST";
    public static string RematchAccept() => "REMATCH_ACCEPT";
    public static string Quit() => "QUIT";
    public static string Error(string reason) => $"ERROR{Separator}{reason}";

    public static bool TryParse(string line, out NetMessageType messageType, out string[] parts)
    {
        messageType = NetMessageType.Unknown;
        parts = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var tokens = line.Split(Separator);
        if (tokens.Length == 0)
        {
            return false;
        }

        switch (tokens[0])
        {
            case "HELLO":
                messageType = NetMessageType.Hello;
                break;
            case "START":
                messageType = NetMessageType.Start;
                break;
            case "MOVE":
                messageType = NetMessageType.Move;
                break;
            case "STATE":
                messageType = NetMessageType.State;
                break;
            case "REMATCH_REQUEST":
                messageType = NetMessageType.RematchRequest;
                break;
            case "REMATCH_ACCEPT":
                messageType = NetMessageType.RematchAccept;
                break;
            case "QUIT":
                messageType = NetMessageType.Quit;
                break;
            case "ERROR":
                messageType = NetMessageType.Error;
                break;
            default:
                return false;
        }

        if (tokens.Length > 1)
        {
            parts = new string[tokens.Length - 1];
            Array.Copy(tokens, 1, parts, 0, parts.Length);
        }

        return true;
    }
}
