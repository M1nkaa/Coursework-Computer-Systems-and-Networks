using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json;
using RPS.Shared;

namespace RPS.Server
{
    public class GameServer
    {
        private TcpListener listener;
        private List<ClientHandler> clients = new List<ClientHandler>();
        private List<GameRoom> gameRooms = new List<GameRoom>(); // НОВОЕ: список игровых комнат
        private bool isRunning;
        private int totalGamesPlayed = 0;

        // События для обновления UI
        public event Action<string, Brush> OnLog;
        public event Action<int, int, int, int> OnStatsUpdate;
        public event Action<List<PlayerInfo>> OnPlayerListUpdate; // НОВОЕ!

        public async Task StartAsync(int port)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                isRunning = true;

                OnLog?.Invoke($"🎮 Сервер запущен на порту {port}", Brushes.LightGreen);

                while (isRunning)
                {
                    try
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        ClientHandler handler = new ClientHandler(client);
                        clients.Add(handler);

                        OnLog?.Invoke($"✅ Новое подключение! IP: {((IPEndPoint)client.Client.RemoteEndPoint).Address}",
                            Brushes.Cyan);

                        UpdateStats();

                        _ = Task.Run(() => HandleClientAsync(handler));
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"❌ Ошибка подключения: {ex.Message}", Brushes.Red);
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"❌ Ошибка запуска сервера: {ex.Message}", Brushes.Red);
            }
        }

        public void Stop()
        {
            isRunning = false;
            listener?.Stop();

            foreach (var client in clients.ToList())
            {
                client.Client?.Close();
            }

            clients.Clear();
            gameRooms.Clear();
        }

        private async Task HandleClientAsync(ClientHandler handler)
        {
            try
            {
                while (isRunning)
                {
                    NetworkMessage message = await handler.ReceiveMessageAsync();
                    if (message == null) break;

                    await ProcessMessageAsync(handler, message);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Ошибка обработки клиента: {ex.Message}", Brushes.Yellow);
            }
            finally
            {
                DisconnectClient(handler);
            }
        }

        private async Task ProcessMessageAsync(ClientHandler handler, NetworkMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Connect:
                    handler.PlayerName = message.PlayerName;
                    OnLog?.Invoke($"👤 {handler.PlayerName} подключился", Brushes.LightBlue);
                    await handler.SendMessageAsync(new NetworkMessage
                    {
                        Type = MessageType.Connect,
                        Data = "Подключение успешно"
                    });
                    UpdateStats();
                    break;

                case MessageType.CreateGame:
                    await CreateGameAsync(handler);
                    break;

                case MessageType.GetGamesList:
                    await SendGamesListAsync(handler);
                    break;

                case MessageType.JoinGame:
                    await JoinGameAsync(handler, message.Data);
                    break;

                case MessageType.MakeChoice:
                    await ProcessChoiceAsync(handler, message);
                    break;

                // НОВОЕ: Обработка выхода из игры
                case MessageType.LeaveGame:
                    await ProcessLeaveGameAsync(handler);
                    break;
            }
        }

        // Обработка выхода игрока из игры
        private async Task ProcessLeaveGameAsync(ClientHandler handler)
        {
            OnLog?.Invoke($"🚪 {handler.PlayerName} вышел из игры", Brushes.Orange);

            // Сохраняем информацию об оппоненте
            ClientHandler opponent = handler.Opponent;

            // Обрабатываем комнату СРАЗУ (до уведомления оппонента)
            var roomAsHost = gameRooms.FirstOrDefault(r => r.HostName == handler.PlayerName);
            var roomAsGuest = gameRooms.FirstOrDefault(r => r.GuestName == handler.PlayerName);

            if (roomAsHost != null)
            {
                // Игрок был хостом - удаляем комнату СРАЗУ
                gameRooms.Remove(roomAsHost);
                OnLog?.Invoke($"🗑️ Игра {handler.PlayerName} удалена", Brushes.Gray);
                await BroadcastGamesListAsync(); // СРАЗУ обновляем список
            }
            else if (roomAsGuest != null)
            {
                // Игрок был гостем - удаляем комнату СРАЗУ (больше не делаем её доступной)
                gameRooms.Remove(roomAsGuest);
                OnLog?.Invoke($"🗑️ Игра {roomAsGuest.HostName} удалена (гость вышел)", Brushes.Gray);
                await BroadcastGamesListAsync(); // СРАЗУ обновляем список
            }

            // Теперь уведомляем оппонента (если он есть)
            if (opponent != null)
            {
                await opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentLeft,
                    Data = $"{handler.PlayerName} покинул игру"
                });

                // Разрываем связь
                opponent.Opponent = null;
                opponent.Score = 0;
            }

            // Разрываем связь у выходящего
            handler.Opponent = null;
            handler.Score = 0;

            UpdateStats();
        }

        // НОВОЕ: Создание игровой комнаты
        private async Task CreateGameAsync(ClientHandler handler)
        {
            // Проверяем, не создал ли уже игрок комнату
            var existingRoom = gameRooms.FirstOrDefault(r => r.HostName == handler.PlayerName);
            if (existingRoom != null)
            {
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.Error,
                    Data = "Вы уже создали игру!"
                });
                return;
            }

            var room = new GameRoom
            {
                HostName = handler.PlayerName
            };

            gameRooms.Add(room);
            handler.RoomId = room.RoomId;

            OnLog?.Invoke($"🎲 {handler.PlayerName} создал игру (ID: {room.RoomId.Substring(0, 8)}...)",
                Brushes.LightGreen);

            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.CreateGame,
                Data = JsonConvert.SerializeObject(room)
            });

            UpdateStats();
            await BroadcastGamesListAsync();
        }

        // НОВОЕ: Отправка списка игр клиенту
        private async Task SendGamesListAsync(ClientHandler handler)
        {
            var availableRooms = gameRooms.Where(r => !r.IsFull).ToList();

            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GamesList,
                Data = JsonConvert.SerializeObject(availableRooms)
            });
        }

        // НОВОЕ: Рассылка списка игр всем клиентам
        private async Task BroadcastGamesListAsync()
        {
            var availableRooms = gameRooms.Where(r => !r.IsFull).ToList();
            var message = new NetworkMessage
            {
                Type = MessageType.GamesList,
                Data = JsonConvert.SerializeObject(availableRooms)
            };

            foreach (var client in clients)
            {
                await client.SendMessageAsync(message);
            }
        }

        // НОВОЕ: Присоединение к игре
        private async Task JoinGameAsync(ClientHandler handler, string roomId)
        {
            var room = gameRooms.FirstOrDefault(r => r.RoomId == roomId);

            if (room == null)
            {
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.Error,
                    Data = "Игра не найдена!"
                });
                return;
            }

            if (room.IsFull)
            {
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.GameFull,
                    Data = "Игра уже заполнена!"
                });
                return;
            }

            // Находим хоста
            var host = clients.FirstOrDefault(c => c.PlayerName == room.HostName);
            if (host == null)
            {
                gameRooms.Remove(room);
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.Error,
                    Data = "Хост отключился!"
                });
                return;
            }

            // Присоединяем игрока
            room.GuestName = handler.PlayerName;
            room.IsFull = true;
            handler.RoomId = room.RoomId;

            // Связываем игроков
            handler.Opponent = host;
            host.Opponent = handler;

            OnLog?.Invoke($"🎲 {handler.PlayerName} присоединился к игре {host.PlayerName}",
                Brushes.LightGreen);

            // Уведомляем хоста
            await host.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameStarted,
                Data = handler.PlayerName
            });

            // Уведомляем гостя
            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameJoined,
                Data = host.PlayerName
            });

            UpdateStats();
            await BroadcastGamesListAsync();
        }

        private async Task ProcessChoiceAsync(ClientHandler handler, NetworkMessage message)
        {
            handler.CurrentChoice = (Choice)Enum.Parse(typeof(Choice), message.Data);

            OnLog?.Invoke($"✋ {handler.PlayerName} выбрал {GetChoiceEmoji(handler.CurrentChoice)}",
                Brushes.White);

            // НОВОЕ: Уведомляем оппонента что игрок сделал выбор
            if (handler.Opponent != null && handler.Opponent.CurrentChoice == Choice.None)
            {
                await handler.Opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentMadeChoice,
                    Data = "Opponent made choice"
                });
            }

            // Проверяем, сделал ли оппонент выбор
            if (handler.Opponent != null && handler.Opponent.CurrentChoice != Choice.None)
            {
                await CalculateResultAsync(handler, handler.Opponent);
            }
        }

        private async Task CalculateResultAsync(ClientHandler player1, ClientHandler player2)
        {
            GameResult result1 = DetermineWinner(player1.CurrentChoice, player2.CurrentChoice);
            GameResult result2 = DetermineWinner(player2.CurrentChoice, player1.CurrentChoice);

            if (result1 == GameResult.Win) player1.Score++;
            if (result2 == GameResult.Win) player2.Score++;

            totalGamesPlayed++;

            string resultEmoji = result1 == GameResult.Win ? "🎉" :
                                result1 == GameResult.Lose ? "😢" : "🤝";

            OnLog?.Invoke($"{resultEmoji} Результат: {player1.PlayerName} ({GetChoiceEmoji(player1.CurrentChoice)}) vs " +
                         $"{player2.PlayerName} ({GetChoiceEmoji(player2.CurrentChoice)})", Brushes.Yellow);

            await SendGameResult(player1, player2, result1);
            await SendGameResult(player2, player1, result2);

            player1.CurrentChoice = Choice.None;
            player2.CurrentChoice = Choice.None;

            UpdateStats();
        }

        private async Task SendGameResult(ClientHandler player, ClientHandler opponent, GameResult result)
        {
            var gameData = new GameData
            {
                PlayerChoice = player.CurrentChoice,
                OpponentChoice = opponent.CurrentChoice,
                Result = result,
                PlayerScore = player.Score,
                OpponentScore = opponent.Score,
                OpponentName = opponent.PlayerName
            };

            await player.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameResult,
                Data = JsonConvert.SerializeObject(gameData)
            });
        }

        private GameResult DetermineWinner(Choice player, Choice opponent)
        {
            if (player == opponent) return GameResult.Draw;

            return (player, opponent) switch
            {
                (Choice.Rock, Choice.Scissors) => GameResult.Win,
                (Choice.Paper, Choice.Rock) => GameResult.Win,
                (Choice.Scissors, Choice.Paper) => GameResult.Win,
                _ => GameResult.Lose
            };
        }

        private string GetChoiceEmoji(Choice choice)
        {
            return choice switch
            {
                Choice.Rock => "🪨",
                Choice.Paper => "📄",
                Choice.Scissors => "✂️",
                _ => "❓"
            };
        }

        private void DisconnectClient(ClientHandler handler)
        {
            // Сохраняем информацию об оппоненте ДО удаления из списка
            ClientHandler opponent = handler.Opponent;
            string playerName = handler.PlayerName ?? "Игрок";

            // Удаляем клиента из списка
            clients.Remove(handler);

            // Обрабатываем игровую комнату
            var roomAsHost = gameRooms.FirstOrDefault(r => r.HostName == handler.PlayerName);
            var roomAsGuest = gameRooms.FirstOrDefault(r => r.GuestName == handler.PlayerName);

            if (roomAsHost != null)
            {
                // Игрок был хостом - удаляем комнату
                if (roomAsHost.IsFull && opponent != null)
                {
                    OnLog?.Invoke($"🚪 {playerName} (хост) отключился от игры с {opponent.PlayerName}", Brushes.Orange);

                    _ = opponent.SendMessageAsync(new NetworkMessage
                    {
                        Type = MessageType.OpponentLeft,
                        Data = $"{playerName} покинул игру"
                    });

                    opponent.Opponent = null;
                    opponent.Score = 0;
                }
                else
                {
                    OnLog?.Invoke($"🗑️ {playerName} удалил пустую игру", Brushes.Gray);
                }

                // Удаляем комнату в ЛЮБОМ случае
                gameRooms.Remove(roomAsHost);
                _ = BroadcastGamesListAsync();
            }
            else if (roomAsGuest != null)
            {
                // Игрок был гостем - удаляем комнату (больше НЕ делаем доступной)
                if (roomAsGuest.IsFull && opponent != null)
                {
                    OnLog?.Invoke($"🚪 {playerName} (гость) отключился от игры с {opponent.PlayerName}", Brushes.Orange);

                    _ = opponent.SendMessageAsync(new NetworkMessage
                    {
                        Type = MessageType.OpponentLeft,
                        Data = $"{playerName} покинул игру"
                    });

                    opponent.Opponent = null;
                    opponent.Score = 0;
                }

                // Удаляем комнату (не делаем снова доступной)
                gameRooms.Remove(roomAsGuest);
                OnLog?.Invoke($"🗑️ Игра удалена (гость {playerName} отключился)", Brushes.Gray);
                _ = BroadcastGamesListAsync();
            }

            OnLog?.Invoke($"❌ {playerName} отключился", Brushes.Red);

            try
            {
                handler.Client?.Close();
            }
            catch { }

            UpdateStats();
        }

        private void UpdateStats()
        {
            int playersOnline = clients.Count;
            int activeGames = gameRooms.Count(r => r.IsFull);
            int waiting = gameRooms.Count(r => !r.IsFull);

            OnStatsUpdate?.Invoke(playersOnline, activeGames, waiting, totalGamesPlayed);

            // НОВОЕ: Обновляем список игроков
            var playerList = new List<PlayerInfo>();
            foreach (var client in clients)
            {
                string status = "В лобби";
                if (client.Opponent != null)
                {
                    status = $"Играет с {client.Opponent.PlayerName}";
                }
                else if (gameRooms.Any(r => r.HostName == client.PlayerName && !r.IsFull))
                {
                    status = "Ожидает соперника";
                }

                playerList.Add(new PlayerInfo
                {
                    PlayerName = client.PlayerName ?? "Неизвестно",
                    Status = status,
                    Score = client.Score
                });
            }

            OnPlayerListUpdate?.Invoke(playerList);
        }
    }
}