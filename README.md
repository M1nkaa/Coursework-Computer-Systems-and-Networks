# ✊✋✌️ Rock Paper Scissors — Multiplayer Network Game

A course project for **Computer Networks and Systems**.  
A real-time multiplayer Rock Paper Scissors game built on a TCP server, WPF client, and a custom JSON messaging protocol.

---

## Tech Stack

| Component | Technologies |
|---|---|
| Language | C# 12, .NET 8 |
| UI | WPF + MaterialDesignInXAML |
| Pattern | MVVM (RelayCommand, INotifyPropertyChanged) |
| Networking | TCP Sockets, async I/O (`async`/`await`) |
| Serialization | Newtonsoft.Json (newline-delimited JSON) |
| IDE | Visual Studio 2022 |

---

## Project Architecture

The solution is composed of three projects:

```
RockPaperScissors.sln
├── RPS.Shared   — shared models, enums, and network protocol
├── RPS.Server   — TCP server with WPF GUI and game logic
└── RPS.Client   — WPF client with MVVM, network service, and UI
```

### RPS.Shared

A class library referenced by both the server and the client.

- **`NetworkMessage`** — universal network packet: `Type`, `PlayerName`, `Data`, `Timestamp`. Serializes to JSON over the wire.
- **`MessageType`** — enum of all 25 protocol message types: `Connect`, `CreateGame`, `MakeChoice`, `GameResult`, `RematchRequest`, `PlayerAfk`, `TimerUpdate`, and more.
- **`Choice`** — player's move: `Rock`, `Paper`, `Scissors`, `None`.
- **`GameResult`** — round outcome: `Win`, `Lose`, `Draw`, `None`.
- **`GameMode`** — `Infinite` (endless rounds) or `FirstTo5` (first to 5 wins).
- **`GameRoom`**, **`GameData`** — room and game session models.

### RPS.Server

A WPF application that hosts the TCP server.

- **`GameServer`** — accepts incoming connections, manages the room list, routes messages between players, and determines round winners.
- **`ClientHandler`** — represents a single connected client: stores name, current choice, score, opponent reference, and manages a **30-second AFK timer** that ticks every second.
- **`MainWindow`** — server GUI: displays an event log and the list of active connections.

### RPS.Client

A WPF application for the player.

- **`NetworkService`** — TCP client: connects to the server, sends messages asynchronously, and listens for incoming packets in a background task. Parses the byte stream into discrete JSON packets using `\n` as a delimiter.
- **`MainViewModel`** — all UI logic: connecting to the server, creating/joining rooms, choosing a move, chat, handling results and rematches.
- **`MainWindow.xaml`** — multi-screen interface (connect, lobby, game) implemented via Visibility bindings.
- **Converters** — `BoolToOpacityConverter`, `InverseBooleanToVisibilityConverter` for XAML data binding.

---

## Messaging Protocol

Client and server communicate over TCP. Each packet is a `NetworkMessage` JSON object terminated by `\n`.

```
[connection]
  Client → Server : Connect { PlayerName }

[room management]
  Client → Server : CreateGame { Data: "RoomName|Mode" }
  Client → Server : GetGamesList
  Server → Client : GamesList { Data: [...] }

[joining a game]
  Client → Server : JoinGame { Data: "RoomId" }
  Server → Client : GameJoined / GameFull
  Server → Both   : GameStarted { Data: "OpponentName" }

[round]
  Server → Both   : AnimationDone  →  starts the move timer
  Server → Both   : TimerUpdate { Data: "29" }  (every second)
  Client → Server : MakeChoice { Data: "Rock" }
  Server → Client : OpponentMadeChoice
  Server → Both   : GameResult { Data: "Win|Lose|Draw|..." }

[rematch / exit]
  Client → Server : RematchRequest
  Server → Both   : RematchAccepted / RematchDeclined
  Client → Server : LeaveGame / Disconnect

[AFK]
  Server → kicked  : PlayerAfk
  Server → other   : OpponentAfk  (awarded the win)

[chat]
  Client → Server : ChatMessage { Data: "text" }
  Server → Client : ChatMessage
```

---

## Features

- **Room browser** — list of active lobbies updates in real time.
- **Two game modes** — endless rounds or a best-of series (first to 5 wins).
- **AFK timer** — if a player doesn't make a move within 30 seconds, they forfeit the round.
- **Rematch** — both players can request a rematch without leaving the room.
- **In-game chat** — text chat available during a match.
- **Material Design UI** — themed interface with animations and a live scoreboard.

---

## Requirements

- **Windows** (WPF application)
- **.NET 8** SDK or Runtime
- Visual Studio 2022 (for building from source)

---

## Getting Started

1. Clone the repository or unpack the archive.
2. Open `RockPaperScissors/RockPaperScissors.sln` in Visual Studio.
3. Build the solution (`Ctrl+Shift+B`).
4. **Start the server** — set `RPS.Server` as the startup project and run it. The server window will display the IP and port it is listening on.
5. **Start one or more clients** — run `RPS.Client` (multiple instances are fine), enter the server IP/port and a player name.

To play on a single machine, use `127.0.0.1` as the server address.

---

## File Structure

```
RockPaperScissors/
├── RPS.Shared/
│   ├── NetworkMessage.cs     # TCP packet + JSON serialization
│   ├── GameModels.cs         # Choice, GameResult, GameMode, MessageType
│   ├── GameRoom.cs           # Room model
│   └── GameData.cs           # Game session data
├── RPS.Server/
│   ├── GameServer.cs         # Core server logic
│   ├── ClientHandler.cs      # Per-client state + AFK timer
│   ├── MainWindow.xaml(.cs)  # Server GUI
│   └── App.xaml(.cs)
└── RPS.Client/
    ├── Services/
    │   └── NetworkService.cs      # TCP client
    ├── ViewModels/
    │   ├── MainViewModel.cs       # All UI logic (MVVM)
    │   └── RelayCommand.cs        # ICommand wrapper
    ├── Converters/                # XAML value converters
    ├── MainWindow.xaml(.cs)       # Main window
    └── App.xaml(.cs)
```

---

## Author

Course project for *Computer Networks and Systems*.
