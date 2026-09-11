using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private GameObject boardBackgroundPrefab;
    [SerializeField] private GameObject boardCellPrefab;
    [SerializeField] private GameObject playerOnePiecePrefab;
    [SerializeField] private GameObject playerTwoPiecePrefab;

    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button[] columnButtons;
    [SerializeField] private Button winPlayAgainButton;
    [SerializeField] private Button losePlayAgainButton;
    [SerializeField] private Button winQuitButton;
    [SerializeField] private Button loseQuitButton;
    [SerializeField] private Button quitOnlyQuitButton;

    [SerializeField] private InputField hostIpInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private InputField playerNameInput;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private TextMeshProUGUI turnStatusText;

    private readonly ConnectFourState _state = new ConnectFourState();
    private readonly List<GameObject> _spawnedBoardVisuals = new List<GameObject>();
    private readonly GameObject[,] _spawnedPieces = new GameObject[ConnectFourState.Width, ConnectFourState.Height];
    private WinsockServer _server;
    private WinsockClient _client;
    private bool _isHost;
    private bool _isConnected;
    private bool _joinModeSelected;
    private bool _localShutdownRequested;
    private bool _hostRematchRequested;
    private bool _clientRematchRequested;
    private bool _localRematchRequested;
    private int _localPlayerId;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        WireUi();
        _state.Reset();
        if (menuManager != null)
        {
            menuManager.ShowConnectMenu();
        }

        if (portInput != null && string.IsNullOrWhiteSpace(portInput.text))
        {
            portInput.text = "7777";
        }

        if (hostIpInput != null && string.IsNullOrWhiteSpace(hostIpInput.text))
        {
            hostIpInput.text = "127.0.0.1";
        }

        SetStatus("Select Host or Join.");
        UpdateTurnStatus();
        UpdateColumnInteractivity();
    }

    // Update is called once per frame
    void Update()
    {
        PollNetworkEvents();
    }

    private void OnDestroy()
    {
        ShutdownNetworking();
    }

    private void WireUi()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(OnHostClicked);
        }

        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinModeClicked);
        }

        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectClicked);
        }

        if (columnButtons != null)
        {
            for (var i = 0; i < columnButtons.Length; i++)
            {
                var columnIndex = i;
                if (columnButtons[i] != null)
                {
                    columnButtons[i].onClick.AddListener(() => OnColumnClicked(columnIndex));
                }
            }
        }

        if (winPlayAgainButton != null)
        {
            winPlayAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }

        if (losePlayAgainButton != null)
        {
            losePlayAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }

        if (winQuitButton != null)
        {
            winQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (loseQuitButton != null)
        {
            loseQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (quitOnlyQuitButton != null)
        {
            quitOnlyQuitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void PollNetworkEvents()
    {
        if (_isHost && _server != null)
        {
            while (_server.TryDequeueEvent(out var netEvent))
            {
                HandleNetworkEvent(netEvent);
            }

            return;
        }

        if (!_isHost && _client != null)
        {
            while (_client.TryDequeueEvent(out var netEvent))
            {
                HandleNetworkEvent(netEvent);
            }
        }
    }

    private void HandleNetworkEvent(NetEvent netEvent)
    {
        switch (netEvent.Type)
        {
            case NetEventType.Connected:
                HandleConnected();
                break;
            case NetEventType.Message:
                HandleMessage(netEvent.Payload);
                break;
            case NetEventType.Disconnected:
                HandleDisconnected();
                break;
            case NetEventType.Error:
                SetStatus(netEvent.Payload);
                break;
        }
    }

    private void HandleConnected()
    {
        if (_isHost)
        {
            _isConnected = true;
            _localPlayerId = 1;
            _hostRematchRequested = false;
            _clientRematchRequested = false;
            _localRematchRequested = false;
            _state.Reset();
            EnsureBoardVisuals();
            RenderBoard();
            if (menuManager != null)
            {
                menuManager.ShowGameUI();
            }

            _server.SendLine(NetMessage.Start(2), out _);
            _server.SendLine(NetMessage.Hello(GetLocalName()), out _);
            BroadcastState();
            SetStatus("Client connected. Your turn.");
            UpdateTurnStatus();
            UpdateColumnInteractivity();
            return;
        }

        _isConnected = true;
        if (_client != null)
        {
            _client.SendLine(NetMessage.Hello(GetLocalName()), out _);
        }

        SetStatus("Connected to host. Waiting for game state.");
    }

    private void HandleMessage(string line)
    {
        if (!NetMessage.TryParse(line, out var messageType, out var parts))
        {
            return;
        }

        switch (messageType)
        {
            case NetMessageType.Start:
                if (!_isHost && parts.Length >= 1 && int.TryParse(parts[0], out var assignedPlayer))
                {
                    _localPlayerId = assignedPlayer;
                    EnsureBoardVisuals();
                    if (menuManager != null)
                    {
                        menuManager.ShowGameUI();
                    }

                    SetStatus($"Match started. You are Player {_localPlayerId}.");
                    UpdateTurnStatus();
                    UpdateColumnInteractivity();
                }

                break;
            case NetMessageType.Move:
                if (_isHost && parts.Length >= 1 && int.TryParse(parts[0], out var column))
                {
                    if (_state.TryDropToken(column, 2, out _))
                    {
                        BroadcastState();
                    }
                    else
                    {
                        if (_server != null)
                        {
                            _server.SendLine(NetMessage.Error("Invalid move."), out _);
                        }
                    }
                }

                break;
            case NetMessageType.State:
                if (parts.Length >= 3 && int.TryParse(parts[1], out var turn) && int.TryParse(parts[2], out var result))
                {
                    if (_state.DecodeAndApply(parts[0], turn, result))
                    {
                        EnsureBoardVisuals();
                        RenderBoard();
                        ApplyStateMenus();
                        UpdateTurnStatus();
                        UpdateColumnInteractivity();
                    }
                }

                break;
            case NetMessageType.RematchRequest:
                if (_isHost)
                {
                    _clientRematchRequested = true;
                    TryStartRematchAsHost();
                }

                break;
            case NetMessageType.RematchAccept:
                _localRematchRequested = false;
                SetStatus("Rematch accepted.");
                break;
            case NetMessageType.Quit:
                HandleOpponentQuit();
                break;
            case NetMessageType.Error:
                if (parts.Length >= 1)
                {
                    SetStatus(parts[0]);
                }

                break;
        }
    }

    private void HandleDisconnected()
    {
        if (_localShutdownRequested)
        {
            return;
        }

        if (_isConnected)
        {
            HandleOpponentQuit();
        }
        else
        {
            SetStatus("Disconnected.");
        }
    }

    private void OnHostClicked()
    {
        ShutdownNetworking();
        _isHost = true;
        _joinModeSelected = false;
        _localPlayerId = 1;
        if (!TryGetPort(out var port))
        {
            SetStatus("Invalid port.");
            return;
        }

        _server = new WinsockServer();
        if (!_server.Start(port, out var error))
        {
            SetStatus(error);
            _server = null;
            return;
        }

        SetStatus($"Hosting on port {port}. Waiting for client...");
        if (menuManager != null)
        {
            menuManager.ShowConnectMenu();
        }
    }

    private void OnJoinModeClicked()
    {
        _joinModeSelected = true;
        _isHost = false;
        SetStatus("Join selected. Enter host IP/port, then click Connect.");
    }

    private void OnConnectClicked()
    {
        if (!_joinModeSelected)
        {
            SetStatus("Select Join first.");
            return;
        }

        ShutdownNetworking();
        _isHost = false;
        _localPlayerId = 0;
        if (!TryGetPort(out var port))
        {
            SetStatus("Invalid port.");
            return;
        }

        var ip = hostIpInput != null ? hostIpInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Enter host IP.");
            return;
        }

        _client = new WinsockClient();
        if (!_client.Connect(ip, port, out var error))
        {
            SetStatus(error);
            _client = null;
            return;
        }
    }

    private void OnColumnClicked(int column)
    {
        if (!_isConnected || _state.Result != ConnectFourResult.Ongoing)
        {
            return;
        }

        if (_state.CurrentTurnPlayer != _localPlayerId)
        {
            return;
        }

        if (_isHost)
        {
            if (_state.TryDropToken(column, 1, out _))
            {
                BroadcastState();
            }
            else
            {
                SetStatus("Invalid move.");
            }

            return;
        }

        if (_client == null)
        {
            return;
        }

        if (!_client.SendLine(NetMessage.Move(column), out var error))
        {
            SetStatus(error);
            return;
        }

        SetStatus("Move sent. Waiting for host...");
        UpdateColumnInteractivity();
    }

    private void OnPlayAgainClicked()
    {
        if (!_isConnected)
        {
            return;
        }

        if (_isHost)
        {
            _hostRematchRequested = true;
            TryStartRematchAsHost();
            return;
        }

        if (_client == null || _localRematchRequested)
        {
            return;
        }

        _localRematchRequested = true;
        if (!_client.SendLine(NetMessage.RematchRequest(), out var error))
        {
            _localRematchRequested = false;
            SetStatus(error);
            return;
        }

        SetStatus("Rematch requested. Waiting for host.");
    }

    private void TryStartRematchAsHost()
    {
        if (!_hostRematchRequested || !_clientRematchRequested)
        {
            SetStatus("Waiting for both players to request rematch.");
            return;
        }

        _hostRematchRequested = false;
        _clientRematchRequested = false;
        _localRematchRequested = false;
        _state.Reset();
        if (_server != null)
        {
            _server.SendLine(NetMessage.RematchAccept(), out _);
        }
        BroadcastState();
        SetStatus("Rematch started.");
    }

    private void OnQuitClicked()
    {
        _localShutdownRequested = true;
        if (_isHost)
        {
            if (_server != null)
            {
                _server.SendLine(NetMessage.Quit(), out _);
            }
        }
        else
        {
            if (_client != null)
            {
                _client.SendLine(NetMessage.Quit(), out _);
            }
        }

        ShutdownNetworking();
        _state.Reset();
        RenderBoard();
        if (menuManager != null)
        {
            menuManager.ShowConnectMenu();
        }

        SetStatus("Connection closed.");
        _localShutdownRequested = false;
    }

    private void HandleOpponentQuit()
    {
        ShutdownNetworking();
        _isConnected = false;
        UpdateColumnInteractivity();
        if (menuManager != null)
        {
            menuManager.ShowQuitOnlyMenu();
        }

        SetStatus("Opponent disconnected.");
    }

    private void ShutdownNetworking()
    {
        _isConnected = false;
        _hostRematchRequested = false;
        _clientRematchRequested = false;
        _localRematchRequested = false;

        if (_server != null)
        {
            _server.Stop();
            _server = null;
        }

        if (_client != null)
        {
            _client.Stop();
            _client = null;
        }
    }

    private void BroadcastState()
    {
        if (!_isHost)
        {
            return;
        }

        var stateLine = NetMessage.State(_state.EncodeBoard(), _state.CurrentTurnPlayer, (int)_state.Result);
        if (_server != null)
        {
            _server.SendLine(stateLine, out _);
        }
        RenderBoard();
        ApplyStateMenus();
        UpdateTurnStatus();
        UpdateColumnInteractivity();
    }

    private void ApplyStateMenus()
    {
        if (menuManager == null)
        {
            return;
        }

        if (_state.Result == ConnectFourResult.Ongoing)
        {
            menuManager.ShowGameUI();
            return;
        }

        if (_state.Result == ConnectFourResult.Draw)
        {
            menuManager.ShowWinMenu();
            SetStatus("Draw game. Play again?");
            return;
        }

        var localWon = (_state.Result == ConnectFourResult.Player1Win && _localPlayerId == 1)
            || (_state.Result == ConnectFourResult.Player2Win && _localPlayerId == 2);
        if (localWon)
        {
            menuManager.ShowWinMenu();
            SetStatus("You win.");
        }
        else
        {
            menuManager.ShowLoseMenu();
            SetStatus("You lose.");
        }
    }

    private void EnsureBoardVisuals()
    {
        if (boardRoot == null)
        {
            return;
        }

        if (_spawnedBoardVisuals.Count > 0)
        {
            return;
        }

        if (boardBackgroundPrefab != null)
        {
            var background = Instantiate(boardBackgroundPrefab, boardRoot);
            var rt = background.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }

            _spawnedBoardVisuals.Add(background);
        }

        if (boardCellPrefab == null)
        {
            return;
        }

        for (var row = 0; row < ConnectFourState.Height; row++)
        {
            for (var column = 0; column < ConnectFourState.Width; column++)
            {
                var cell = Instantiate(boardCellPrefab, boardRoot);
                PositionRect(cell, column, row);
                _spawnedBoardVisuals.Add(cell);
            }
        }
    }

    private void RenderBoard()
    {
        if (boardRoot == null)
        {
            return;
        }

        for (var row = 0; row < ConnectFourState.Height; row++)
        {
            for (var column = 0; column < ConnectFourState.Width; column++)
            {
                var value = _state.Board[column, row];
                var existing = _spawnedPieces[column, row];
                if (value == 0)
                {
                    if (existing != null)
                    {
                        Destroy(existing);
                        _spawnedPieces[column, row] = null;
                    }

                    continue;
                }

                if (existing != null)
                {
                    continue;
                }

                var prefab = value == 1 ? playerOnePiecePrefab : playerTwoPiecePrefab;
                if (prefab == null)
                {
                    continue;
                }

                var piece = Instantiate(prefab, boardRoot);
                PositionRect(piece, column, row);
                _spawnedPieces[column, row] = piece;
            }
        }
    }

    private void PositionRect(GameObject gameObject, int column, int row)
    {
        if (gameObject == null || boardRoot == null)
        {
            return;
        }

        var rect = gameObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        var boardWidth = boardRoot.rect.width;
        var boardHeight = boardRoot.rect.height;
        var cellWidth = boardWidth / ConnectFourState.Width;
        var cellHeight = boardHeight / ConnectFourState.Height;
        var x = -boardWidth * 0.5f + cellWidth * 0.5f + column * cellWidth;
        var y = -boardHeight * 0.5f + cellHeight * 0.5f + row * cellHeight;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private void UpdateColumnInteractivity()
    {
        var allow = _isConnected && _state.Result == ConnectFourResult.Ongoing && _state.CurrentTurnPlayer == _localPlayerId;
        if (columnButtons == null)
        {
            return;
        }

        for (var i = 0; i < columnButtons.Length; i++)
        {
            if (columnButtons[i] != null)
            {
                columnButtons[i].interactable = allow;
            }
        }
    }

    private void UpdateTurnStatus()
    {
        if (turnStatusText == null)
        {
            return;
        }

        if (!_isConnected)
        {
            turnStatusText.text = "Not connected.";
            return;
        }

        if (_state.Result != ConnectFourResult.Ongoing)
        {
            turnStatusText.text = "Game over.";
            return;
        }

        turnStatusText.text = _state.CurrentTurnPlayer == _localPlayerId ? "Your turn." : "Opponent turn.";
    }

    private void SetStatus(string message)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
    }

    private bool TryGetPort(out int port)
    {
        port = 0;
        if (portInput == null)
        {
            return false;
        }

        return int.TryParse(portInput.text, out port) && port is > 0 and < 65536;
    }

    private string GetLocalName()
    {
        if (playerNameInput == null || string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            return "Player";
        }

        return playerNameInput.text.Trim();
    }
}
