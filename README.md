# Multiplayer Connect Four (Unity 2D)

Simple 1v1 turn-based Connect Four built in Unity Canvas UI with custom Winsock networking (host/client).

## Gameplay
- Two players take turns dropping pieces into one of 7 columns.
- First player to connect 4 pieces (horizontal, vertical, or diagonal) wins.
- If the board fills with no connect-4, the game ends in a draw.
- End menus allow rematch or exiting the lobby.

## How to Connect
1. **Host player**
   - Enter a port (example: `7777`)
   - Press **Host**
2. **Join player**
   - Enter host IP (for same machine/local test, use `127.0.0.1`)
   - Enter the same port
   - Press **Join**

## Suggested Playtest Method (Single Device)
- Run the game in the **Unity Editor**.
- Build and run the **standalone executable** at the same time.
- Use one instance as Host and the other as Join to test full multiplayer flow locally.
