using System;
using System.Text;

public enum ConnectFourResult
{
    Ongoing = 0,
    Player1Win = 1,
    Player2Win = 2,
    Draw = 3
}

public class ConnectFourState
{
    public const int Width = 7;
    public const int Height = 6;
    public int[,] Board { get; } = new int[Width, Height];
    public int CurrentTurnPlayer { get; private set; } = 1;
    public ConnectFourResult Result { get; private set; } = ConnectFourResult.Ongoing;

    public void Reset()
    {
        Array.Clear(Board, 0, Board.Length);
        CurrentTurnPlayer = 1;
        Result = ConnectFourResult.Ongoing;
    }

    public bool TryDropToken(int column, int playerId, out int placedRow)
    {
        placedRow = -1;
        if (Result != ConnectFourResult.Ongoing)
        {
            return false;
        }

        if (column < 0 || column >= Width)
        {
            return false;
        }

        if (playerId != CurrentTurnPlayer)
        {
            return false;
        }

        for (var row = 0; row < Height; row++)
        {
            if (Board[column, row] != 0)
            {
                continue;
            }

            Board[column, row] = playerId;
            placedRow = row;
            if (IsWinningMove(column, row, playerId))
            {
                Result = playerId == 1 ? ConnectFourResult.Player1Win : ConnectFourResult.Player2Win;
                return true;
            }

            if (IsBoardFull())
            {
                Result = ConnectFourResult.Draw;
                return true;
            }

            CurrentTurnPlayer = CurrentTurnPlayer == 1 ? 2 : 1;
            return true;
        }

        return false;
    }

    public string EncodeBoard()
    {
        var builder = new StringBuilder(Width * Height);
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                builder.Append(Board[column, row]);
            }
        }

        return builder.ToString();
    }

    public bool DecodeAndApply(string payload, int currentTurn, int result)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length != Width * Height)
        {
            return false;
        }

        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                var index = row * Width + column;
                var cellChar = payload[index];
                if (cellChar < '0' || cellChar > '2')
                {
                    return false;
                }

                Board[column, row] = cellChar - '0';
            }
        }

        CurrentTurnPlayer = currentTurn is 1 or 2 ? currentTurn : 1;
        Result = Enum.IsDefined(typeof(ConnectFourResult), result)
            ? (ConnectFourResult)result
            : ConnectFourResult.Ongoing;
        return true;
    }

    private bool IsWinningMove(int column, int row, int playerId)
    {
        return CountConnected(column, row, 1, 0, playerId) >= 4
            || CountConnected(column, row, 0, 1, playerId) >= 4
            || CountConnected(column, row, 1, 1, playerId) >= 4
            || CountConnected(column, row, 1, -1, playerId) >= 4;
    }

    private int CountConnected(int column, int row, int deltaX, int deltaY, int playerId)
    {
        var count = 1;
        count += CountDirection(column, row, deltaX, deltaY, playerId);
        count += CountDirection(column, row, -deltaX, -deltaY, playerId);
        return count;
    }

    private int CountDirection(int column, int row, int deltaX, int deltaY, int playerId)
    {
        var count = 0;
        var x = column + deltaX;
        var y = row + deltaY;
        while (x >= 0 && x < Width && y >= 0 && y < Height && Board[x, y] == playerId)
        {
            count++;
            x += deltaX;
            y += deltaY;
        }

        return count;
    }

    private bool IsBoardFull()
    {
        for (var column = 0; column < Width; column++)
        {
            if (Board[column, Height - 1] == 0)
            {
                return false;
            }
        }

        return true;
    }
}
