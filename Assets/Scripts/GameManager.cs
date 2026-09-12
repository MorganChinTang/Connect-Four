using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private GameObject boardBackgroundPrefab;
    [SerializeField] private GameObject boardCellPrefab;
    [SerializeField] private GameObject playerOnePiecePrefab;
    [SerializeField] private GameObject playerTwoPiecePrefab;
    [SerializeField] private GameObject playerOnePiecePreviewPrefab;
    [SerializeField] private GameObject playerTwoPiecePreviewPrefab;

    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button[] columnButtons;
    [SerializeField] private Button winPlayAgainButton;
    [SerializeField] private Button losePlayAgainButton;
    [SerializeField] private Button drawPlayAgainButton;
    [SerializeField] private Button winQuitButton;
    [SerializeField] private Button loseQuitButton;
    [SerializeField] private Button drawQuitButton;
    [SerializeField] private Button quitOnlyQuitButton;
    [SerializeField] private Button quitAppButton;
    [SerializeField] private Button SingleplayerButton;

    [SerializeField] private TMP_InputField hostIpInput;
    [SerializeField] private TMP_InputField portInput;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private TextMeshProUGUI turnStatusText;

    private readonly ConnectFourState _state = new ConnectFourState();
    private readonly List<GameObject> _spawnedBoardVisuals = new List<GameObject>();
    private readonly GameObject[,] _spawnedPieces = new GameObject[ConnectFourState.Width, ConnectFourState.Height];
    private WinsockServer _server;
    private WinsockClient _client;
    private bool _isHost;
    private bool _isConnected;
    private bool _localShutdownRequested;
    private bool _hostRematchRequested;
    private bool _clientRematchRequested;
    private bool _localRematchRequested;
    private bool _singlePlayerDebugSessionActive;
    private int _localPlayerId;
    private int _hoveredColumn = -1;
    private GameObject _previewPiece;

    public bool SinglePlayerDebugMode = false;

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

        if (SingleplayerButton != null)
        {
            SingleplayerButton.gameObject.SetActive(SinglePlayerDebugMode);
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
                    SetupColumnHover(columnButtons[i], columnIndex);
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

        if (drawPlayAgainButton != null)
        {
            drawPlayAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }

        if (winQuitButton != null)
        {
            winQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (loseQuitButton != null)
        {
            loseQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (drawQuitButton != null)
        {
            drawQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (quitOnlyQuitButton != null)
        {
            quitOnlyQuitButton.onClick.AddListener(OnQuitClicked);
        }

        if (quitAppButton != null)
        {
            quitAppButton.onClick.AddListener(OnQuitAppClick);
        }

        if (SingleplayerButton != null)
        {
            SingleplayerButton.onClick.AddListener(OnSinglePlayerClicked);
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
            _server.SendLine(NetMessage.Hello("Player"), out _);
            BroadcastState();
            SetStatus("Client connected. Your turn.");
            UpdateTurnStatus();
            UpdateColumnInteractivity();
            UpdatePreviewPiece();
            return;
        }

        _isConnected = true;
        if (_client != null)
        {
            _client.SendLine(NetMessage.Hello("Player"), out _);
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
                        UpdatePreviewPiece();
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
        OnConnectClicked();
    }

    private void OnConnectClicked()
    {
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

        if (_singlePlayerDebugSessionActive)
        {
            if (_state.TryDropToken(column, _state.CurrentTurnPlayer, out _))
            {
                RenderBoard();
                ApplyStateMenus();
                UpdateTurnStatus();
                UpdateColumnInteractivity();
                UpdatePreviewPiece();
            }
            else
            {
                SetStatus("Invalid move.");
            }

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
        UpdatePreviewPiece();
    }

    private void OnPlayAgainClicked()
    {
        if (!_isConnected)
        {
            return;
        }

        if (_singlePlayerDebugSessionActive)
        {
            _state.Reset();
            RenderBoard();
            if (menuManager != null)
            {
                menuManager.ShowGameUI();
            }

            SetStatus("Single-player debug match reset.");
            UpdateTurnStatus();
            UpdateColumnInteractivity();
            UpdatePreviewPiece();
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

    private void OnQuitAppClick()
    {
        Application.Quit();
    }

    private void OnSinglePlayerClicked()
    {
        if (!SinglePlayerDebugMode)
        {
            return;
        }

        ShutdownNetworking();
        _singlePlayerDebugSessionActive = true;
        _isHost = false;
        _isConnected = true;
        _localPlayerId = 1;
        _state.Reset();
        EnsureBoardVisuals();
        RenderBoard();
        if (menuManager != null)
        {
            menuManager.ShowGameUI();
        }

        SetStatus("Single-player debug mode: play both sides.");
        UpdateTurnStatus();
        UpdateColumnInteractivity();
        UpdatePreviewPiece();
    }

    private void OnQuitClicked()
    {
        _localShutdownRequested = true;
        var wasDebugSession = _singlePlayerDebugSessionActive;
        _singlePlayerDebugSessionActive = false;
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
        HidePreviewPiece();
        if (menuManager != null)
        {
            menuManager.ShowConnectMenu();
        }

        SetStatus(wasDebugSession ? "Single-player debug mode ended." : "Connection closed.");
        _localShutdownRequested = false;
    }

    private void HandleOpponentQuit()
    {
        ShutdownNetworking();
        _isConnected = false;
        UpdateColumnInteractivity();
        HidePreviewPiece();
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
        _singlePlayerDebugSessionActive = false;

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
        UpdatePreviewPiece();
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
            menuManager.ShowDrawMenu();
            SetStatus("Draw game. Play again?");
            return;
        }

        if (_singlePlayerDebugSessionActive)
        {
            menuManager.ShowWinMenu();
            SetStatus(_state.Result == ConnectFourResult.Player1Win ? "Player 1 wins." : "Player 2 wins.");
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
        var allow = _singlePlayerDebugSessionActive
            ? _isConnected && _state.Result == ConnectFourResult.Ongoing
            : _isConnected && _state.Result == ConnectFourResult.Ongoing && _state.CurrentTurnPlayer == _localPlayerId;
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

        if (!allow)
        {
            HidePreviewPiece();
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

        if (_singlePlayerDebugSessionActive)
        {
            turnStatusText.text = _state.CurrentTurnPlayer == 1 ? "Player 1 turn." : "Player 2 turn.";
            return;
        }

        turnStatusText.text = _state.CurrentTurnPlayer == _localPlayerId ? "Your turn." : "Opponent turn.";
    }

    private void SetupColumnHover(Button button, int columnIndex)
    {
        var trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        AddEventTrigger(trigger, EventTriggerType.PointerEnter, _ => OnColumnHoverEnter(columnIndex));
        AddEventTrigger(trigger, EventTriggerType.PointerExit, _ => OnColumnHoverExit(columnIndex));
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }

        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void OnColumnHoverEnter(int column)
    {
        _hoveredColumn = column;
        UpdatePreviewPiece();
    }

    private void OnColumnHoverExit(int column)
    {
        if (_hoveredColumn != column)
        {
            return;
        }

        _hoveredColumn = -1;
        HidePreviewPiece();
    }

    private void UpdatePreviewPiece()
    {
        if (boardRoot == null
            || _hoveredColumn < 0
            || _hoveredColumn >= ConnectFourState.Width
            || !_isConnected
            || _state.Result != ConnectFourResult.Ongoing)
        {
            HidePreviewPiece();
            return;
        }

        var allow = _singlePlayerDebugSessionActive || _state.CurrentTurnPlayer == _localPlayerId;
        if (!allow)
        {
            HidePreviewPiece();
            return;
        }

        var row = _state.GetDropRow(_hoveredColumn);
        if (row < 0)
        {
            HidePreviewPiece();
            return;
        }

        var previewPrefab = GetPreviewPrefabForCurrentTurn();
        if (previewPrefab == null)
        {
            HidePreviewPiece();
            return;
        }

        if (_previewPiece == null || _previewPiece.name.Replace("(Clone)", string.Empty).Trim() != previewPrefab.name)
        {
            HidePreviewPiece();
            _previewPiece = Instantiate(previewPrefab, boardRoot);
        }

        PositionRect(_previewPiece, _hoveredColumn, row);
    }

    private GameObject GetPreviewPrefabForCurrentTurn()
    {
        if (_state.CurrentTurnPlayer == 1)
        {
            return playerOnePiecePreviewPrefab != null ? playerOnePiecePreviewPrefab : playerOnePiecePrefab;
        }

        return playerTwoPiecePreviewPrefab != null ? playerTwoPiecePreviewPrefab : playerTwoPiecePrefab;
    }

    private void HidePreviewPiece()
    {
        if (_previewPiece != null)
        {
            Destroy(_previewPiece);
            _previewPiece = null;
        }
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

}
