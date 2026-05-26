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
    // Основной класс сервера: принимает TCP-соединения, маршрутизирует
    // сообщения между клиентами и управляет состоянием всех игровых комнат
    public class GameServer
    {
        private TcpListener listener;
        private List<ClientHandler> clients = new List<ClientHandler>();
        private List<GameRoom> gameRooms = new List<GameRoom>();
        private bool isRunning;
        private int totalGamesPlayed = 0;

        // События для обновления UI серверного окна
        public event Action<string, Brush> OnLog;
        public event Action<int, int, int, int> OnStatsUpdate;
        public event Action<List<PlayerInfo>> OnPlayerListUpdate;

        // Запускает прослушивание на указанном порту и в цикле принимает клиентов
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
                        var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;
                        ClientHandler handler = new ClientHandler(client);
                        lock (clients) clients.Add(handler);
                        OnLog?.Invoke($"✅ Новое подключение — IP: {endpoint.Address}:{endpoint.Port}", Brushes.Cyan);
                        UpdateStats();
                        _ = Task.Run(() => HandleClientAsync(handler));
                    }
                    catch (ObjectDisposedException) { break; }
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

        // Останавливает сервер и закрывает все активные соединения
        public void Stop()
        {
            isRunning = false;
            listener?.Stop();
            lock (clients)
            {
                foreach (var c in clients.ToList()) c.Client?.Close();
                clients.Clear();
            }
            gameRooms.Clear();
        }

        // Читает сообщения от одного клиента в цикле до разрыва соединения
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
                OnLog?.Invoke($"⚠️ Ошибка клиента {handler.PlayerName ?? "?"}: {ex.Message}", Brushes.Yellow);
            }
            finally
            {
                DisconnectClient(handler);
            }
        }

        // Диспетчер входящих сообщений: валидирует и направляет в нужный обработчик
        private async Task ProcessMessageAsync(ClientHandler handler, NetworkMessage message)
        {
            // Валидация имени игрока для игровых сообщений
            if (message.Type != MessageType.Connect && string.IsNullOrWhiteSpace(message.PlayerName))
            {
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.Error,
                    Data = "Имя игрока не указано."
                });
                return;
            }

            switch (message.Type)
            {
                case MessageType.Connect:
                    // Валидация имени
                    string name = (message.PlayerName ?? "").Trim();
                    if (string.IsNullOrEmpty(name) || name.Length > 20)
                    {
                        await handler.SendMessageAsync(new NetworkMessage
                        {
                            Type = MessageType.Error,
                            Data = "Имя должно быть от 1 до 20 символов."
                        });
                        return;
                    }
                    // Проверка дублирования имён
                    bool nameTaken;
                    lock (clients) nameTaken = clients.Any(c => c != handler && c.PlayerName == name);
                    if (nameTaken)
                    {
                        await handler.SendMessageAsync(new NetworkMessage
                        {
                            Type = MessageType.Error,
                            Data = $"Имя «{name}» уже занято. Выберите другое."
                        });
                        return;
                    }
                    handler.PlayerName = name;
                    OnLog?.Invoke($"👤 Игрок «{name}» подключился", Brushes.LightBlue);
                    await handler.SendMessageAsync(new NetworkMessage
                    {
                        Type = MessageType.Connect,
                        Data = "Подключение успешно"
                    });
                    UpdateStats();
                    break;

                case MessageType.CreateGame:
                    await CreateGameAsync(handler, message.Data);
                    break;

                case MessageType.GetGamesList:
                    await SendGamesListAsync(handler);
                    break;

                case MessageType.JoinGame:
                    if (string.IsNullOrWhiteSpace(message.Data))
                    {
                        await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Не указан ID комнаты." });
                        return;
                    }
                    await JoinGameAsync(handler, message.Data);
                    break;

                case MessageType.MakeChoice:
                    if (!Enum.TryParse<Choice>(message.Data, out var parsedChoice) || parsedChoice == Choice.None)
                    {
                        await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Неверный выбор фигуры." });
                        return;
                    }
                    await ProcessChoiceAsync(handler, parsedChoice);
                    break;

                case MessageType.LeaveGame:
                    await ProcessLeaveGameAsync(handler);
                    break;

                case MessageType.RematchRequest:
                    await ProcessRematchRequestAsync(handler);
                    break;

                case MessageType.AnimationDone:
                    await ProcessAnimationDoneAsync(handler);
                    break;
            }
        }

        // --- Создание игры ---
        private async Task CreateGameAsync(ClientHandler handler, string modeData)
        {
            // Проверяем, не в игре ли уже
            if (handler.Opponent != null)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Вы уже в игре!" });
                return;
            }

            var existingRoom = gameRooms.FirstOrDefault(r => r.HostName == handler.PlayerName);
            if (existingRoom != null)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Вы уже создали игру!" });
                return;
            }

            GameMode mode = GameMode.Infinite;
            if (!string.IsNullOrWhiteSpace(modeData))
                Enum.TryParse<GameMode>(modeData, out mode);

            var room = new GameRoom
            {
                HostName = handler.PlayerName,
                Mode = mode
            };
            gameRooms.Add(room);
            handler.RoomId = room.RoomId;
            handler.RoomMode = mode;

            string modeLabel = mode == GameMode.FirstTo5 ? "первый до 5 побед" : "бесконечный";
            OnLog?.Invoke($"🎲 «{handler.PlayerName}» создал игру [{modeLabel}] (ID: {room.RoomId.Substring(0, 8)}…)", Brushes.LightGreen);

            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.CreateGame,
                Data = JsonConvert.SerializeObject(room)
            });

            UpdateStats();
            await BroadcastGamesListAsync();
        }

        // --- Список игр ---

        // Отправляет конкретному игроку список незаполненных комнат
        private async Task SendGamesListAsync(ClientHandler handler)
        {
            var available = gameRooms.Where(r => !r.IsFull).ToList();
            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GamesList,
                Data = JsonConvert.SerializeObject(available)
            });
        }

        // Рассылает обновлённый список комнат всем подключённым клиентам
        private async Task BroadcastGamesListAsync()
        {
            var available = gameRooms.Where(r => !r.IsFull).ToList();
            var msg = new NetworkMessage
            {
                Type = MessageType.GamesList,
                Data = JsonConvert.SerializeObject(available)
            };
            List<ClientHandler> snapshot;
            lock (clients) snapshot = clients.ToList();
            foreach (var c in snapshot)
                await c.SendMessageAsync(msg);
        }

        // --- Присоединение ---
        private async Task JoinGameAsync(ClientHandler handler, string roomId)
        {
            if (handler.Opponent != null)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Вы уже в игре!" });
                return;
            }

            var room = gameRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Игра не найдена!" });
                return;
            }
            if (room.IsFull)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.GameFull, Data = "Игра уже заполнена!" });
                return;
            }
            if (room.HostName == handler.PlayerName)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Нельзя присоединиться к своей игре!" });
                return;
            }

            ClientHandler host;
            lock (clients) host = clients.FirstOrDefault(c => c.PlayerName == room.HostName);
            if (host == null)
            {
                gameRooms.Remove(room);
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Хост отключился!" });
                return;
            }

            // Связываем двух игроков и сбрасываем счёт
            room.GuestName = handler.PlayerName;
            room.IsFull = true;
            handler.RoomId = room.RoomId;
            handler.RoomMode = room.Mode;

            host.Opponent = handler;
            handler.Opponent = host;
            handler.AnimationReady = false;
            host.AnimationReady = false;

            host.Score = 0;
            handler.Score = 0;

            string modeLabel = room.Mode == GameMode.FirstTo5 ? "первый до 5 побед" : "бесконечный";
            OnLog?.Invoke($"🎲 «{handler.PlayerName}» присоединился к игре «{host.PlayerName}» [{modeLabel}]", Brushes.LightGreen);

            string modeJson = room.Mode.ToString();

            await host.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameStarted,
                Data = $"{handler.PlayerName}|{modeJson}"
            });

            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameJoined,
                Data = $"{host.PlayerName}|{modeJson}"
            });

            UpdateStats();
            await BroadcastGamesListAsync();

            // Запускаем AFK-таймеры для обоих
            StartAfkTimersForBoth(host, handler);
        }

        // --- AFK таймеры ---

        private void StartAfkTimersForBoth(ClientHandler p1, ClientHandler p2)
        {
            StartAfkTimer(p1);
            StartAfkTimer(p2);
        }

        // Запускает AFK-таймер для одного игрока: тикает каждую секунду,
        // при таймауте — кикает игрока
        private void StartAfkTimer(ClientHandler handler)
        {
            handler.StartAfkTimer(
                onTick: async (secondsLeft) =>
                {
                    await handler.SendMessageAsync(new NetworkMessage
                    {
                        Type = MessageType.TimerUpdate,
                        Data = secondsLeft.ToString()
                    });
                },
                onTimeout: async () =>
                {
                    OnLog?.Invoke($"⏱️ «{handler.PlayerName}» кикнут за AFK (30 сек)", Brushes.Orange);
                    await KickPlayerAfk(handler);
                }
            );
        }

        private async Task KickPlayerAfk(ClientHandler handler)
        {
            // Атомарно захватываем оппонента и разрываем связь.
            // lock гарантирует что два параллельных таймера не могут оба
            // решить что являются «победителями» друг над другом.
            ClientHandler opponent;
            bool alreadyHandled;
            lock (clients)
            {
                if (handler.Opponent == null)
                {
                    // Параллельный вызов уже всё сделал — нам осталось
                    // только отправить PlayerAfk этому игроку
                    opponent = null;
                    alreadyHandled = true;
                }
                else
                {
                    opponent = handler.Opponent;
                    alreadyHandled = false;

                    // Оба AFK одновременно: оппонент уже занулил свой Opponent
                    bool opponentAlsoAfk = opponent.Opponent == null;

                    handler.Opponent = null;
                    if (!opponentAlsoAfk)
                        opponent.Opponent = null;
                    else
                        opponent = null; // победителя нет — не шлём OpponentAfk
                }
            }

            handler.CancelAfkTimer();
            if (opponent != null) opponent.CancelAfkTimer();

            // Сообщаем кикнутому — остаётся подключённым, возвращается в лобби
            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.PlayerAfk,
                Data = "Вы были отключены за бездействие (AFK)."
            });

            if (alreadyHandled) return; // комната и статистика уже обновлены первым вызовом

            // Сообщаем оппоненту — он победитель (только если он сам не AFK)
            if (opponent != null)
            {
                await opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentAfk,
                    Data = $"{handler.PlayerName} был отключён за AFK — вы победили!"
                });
                opponent.Score = 0;
            }

            CleanupRoom(handler);
            handler.Score = 0;

            UpdateStats();
            await BroadcastGamesListAsync();
        }

        // --- Готовность после анимации ---

        // Когда оба клиента закончили анимацию — запускаем таймеры нового раунда
        private async Task ProcessAnimationDoneAsync(ClientHandler handler)
        {
            handler.AnimationReady = true;
            var opponent = handler.Opponent;

            if (opponent != null && opponent.AnimationReady)
            {
                // Оба завершили анимацию — сбрасываем флаги и запускаем таймеры
                handler.AnimationReady = false;
                opponent.AnimationReady = false;
                OnLog?.Invoke($"▶️ Оба игрока готовы к следующему раунду, запускаем таймеры", Brushes.Gray);
                StartAfkTimer(handler);
                StartAfkTimer(opponent);
            }
            // Если только один — ждём второго
        }

        // --- Реванш ---
        private async Task ProcessRematchRequestAsync(ClientHandler handler)
        {
            handler.WantsRematch = true;
            var opponent = handler.Opponent;

            if (opponent == null)
            {
                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.RematchDeclined,
                    Data = "Соперник уже покинул игру."
                });
                handler.WantsRematch = false;
                return;
            }

            // Показываем обоим счётчик готовности (1/2 или 2/2)
            int readyCount = (handler.WantsRematch ? 1 : 0) + (opponent.WantsRematch ? 1 : 0);
            string countLabel = $"{readyCount}/2";

            OnLog?.Invoke($"🔄 «{handler.PlayerName}» хочет реванш [{countLabel}]", Brushes.Cyan);

            await handler.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.RematchRequest,
                Data = countLabel
            });
            await opponent.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.RematchRequest,
                Data = countLabel
            });

            if (opponent.WantsRematch)
            {
                // Оба готовы — сбрасываем состояние и стартуем новую серию
                handler.WantsRematch = false;
                opponent.WantsRematch = false;

                handler.CurrentChoice = Choice.None;
                opponent.CurrentChoice = Choice.None;
                handler.Score = 0;
                opponent.Score = 0;
                handler.AnimationReady = false;
                opponent.AnimationReady = false;

                GameMode mode = handler.RoomMode;
                string modeJson = mode.ToString();

                OnLog?.Invoke($"🎮 Реванш! «{handler.PlayerName}» vs «{opponent.PlayerName}» [{modeJson}]", Brushes.LightGreen);

                await handler.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.GameStarted,
                    Data = $"{opponent.PlayerName}|{modeJson}"
                });

                await opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.GameJoined,
                    Data = $"{handler.PlayerName}|{modeJson}"
                });

                UpdateStats();
                StartAfkTimersForBoth(handler, opponent);
            }
        }

        private async Task ProcessLeaveGameAsync(ClientHandler handler)
        {
            OnLog?.Invoke($"🚪 «{handler.PlayerName}» вышел из игры", Brushes.Orange);

            handler.CancelAfkTimer();
            handler.WantsRematch = false;
            var opponent = handler.Opponent;
            if (opponent != null) { opponent.CancelAfkTimer(); opponent.WantsRematch = false; }

            CleanupRoom(handler);

            if (opponent != null)
            {
                await opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentLeft,
                    Data = $"{handler.PlayerName} покинул игру"
                });
                opponent.Opponent = null;
                opponent.Score = 0;
            }

            handler.Opponent = null;
            handler.Score = 0;

            UpdateStats();
        }

        // Удаляет комнату, в которой участвовал игрок (хост или гость)
        private void CleanupRoom(ClientHandler handler)
        {
            var roomAsHost = gameRooms.FirstOrDefault(r => r.HostName == handler.PlayerName);
            var roomAsGuest = gameRooms.FirstOrDefault(r => r.GuestName == handler.PlayerName);

            if (roomAsHost != null)
            {
                gameRooms.Remove(roomAsHost);
                OnLog?.Invoke($"🗑️ Комната «{handler.PlayerName}» удалена", Brushes.Gray);
            }
            else if (roomAsGuest != null)
            {
                gameRooms.Remove(roomAsGuest);
                OnLog?.Invoke($"🗑️ Комната «{roomAsGuest.HostName}» удалена (гость вышел)", Brushes.Gray);
            }
            _ = BroadcastGamesListAsync();
        }

        // --- Обработка хода ---
        private async Task ProcessChoiceAsync(ClientHandler handler, Choice choice)
        {
            if (handler.Opponent == null)
            {
                await handler.SendMessageAsync(new NetworkMessage { Type = MessageType.Error, Data = "Нет активного соперника." });
                return;
            }
            if (handler.CurrentChoice != Choice.None) return; // уже сделал выбор

            handler.CurrentChoice = choice;
            // Останавливаем AFK-таймер этого игрока
            handler.CancelAfkTimer();

            OnLog?.Invoke(
                $"✋ «{handler.PlayerName}» выбрал {GetChoiceEmoji(choice)} | " +
                $"соперник: {(handler.Opponent.CurrentChoice != Choice.None ? "✅ уже выбрал" : "⏳ ждём")}",
                Brushes.White);

            if (handler.Opponent.CurrentChoice == Choice.None)
            {
                // Уведомляем соперника, что ход уже сделан
                await handler.Opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentMadeChoice,
                    Data = "Соперник сделал ход — ваш черёд!"
                });
            }
            else
            {
                // Оба выбрали — считаем результат
                handler.Opponent.CancelAfkTimer();
                await CalculateResultAsync(handler, handler.Opponent);
            }
        }

        // Определяет победителя раунда, обновляет счёт и отправляет результат обоим игрокам
        private async Task CalculateResultAsync(ClientHandler player1, ClientHandler player2)
        {
            GameResult result1 = DetermineWinner(player1.CurrentChoice, player2.CurrentChoice);
            GameResult result2 = DetermineWinner(player2.CurrentChoice, player1.CurrentChoice);

            if (result1 == GameResult.Win) player1.Score++;
            if (result2 == GameResult.Win) player2.Score++;

            totalGamesPlayed++;

            string emoji = result1 == GameResult.Win ? "🎉" : result1 == GameResult.Lose ? "😢" : "🤝";
            OnLog?.Invoke(
                $"{emoji} Раунд #{totalGamesPlayed}: «{player1.PlayerName}» {GetChoiceEmoji(player1.CurrentChoice)} vs " +
                $"«{player2.PlayerName}» {GetChoiceEmoji(player2.CurrentChoice)} → " +
                $"счёт {player1.Score}:{player2.Score}",
                Brushes.Yellow);

            // Проверяем режим FirstTo5
            bool isGameOver = false;
            string gameOverWinner = null;

            if (player1.RoomMode == GameMode.FirstTo5)
            {
                if (player1.Score >= 5)
                {
                    isGameOver = true;
                    gameOverWinner = player1.PlayerName;
                    OnLog?.Invoke($"🏆 Серия завершена! Победитель: «{player1.PlayerName}» (5:{player2.Score})", Brushes.LightGreen);
                }
                else if (player2.Score >= 5)
                {
                    isGameOver = true;
                    gameOverWinner = player2.PlayerName;
                    OnLog?.Invoke($"🏆 Серия завершена! Победитель: «{player2.PlayerName}» ({player1.Score}:5)", Brushes.LightGreen);
                }
            }

            await SendGameResult(player1, player2, result1, isGameOver, gameOverWinner);
            await SendGameResult(player2, player1, result2, isGameOver, gameOverWinner);

            // Сбрасываем ходы для следующего раунда
            player1.CurrentChoice = Choice.None;
            player2.CurrentChoice = Choice.None;

            if (isGameOver)
            {
                // Серия окончена — отменяем таймеры и чистим комнату,
                // но НЕ обнуляем Opponent — он нужен для реванша
                player1.CancelAfkTimer();
                player2.CancelAfkTimer();
                CleanupRoom(player1);
            }
            else
            {
                // Следующий раунд — таймеры запустятся когда оба клиента закончат анимацию (AnimationDone)
            }

            UpdateStats();
        }

        // Упаковывает результат раунда и отправляет конкретному игроку
        private async Task SendGameResult(ClientHandler player, ClientHandler opponent, GameResult result, bool isGameOver, string gameOverWinner)
        {
            var gameData = new GameData
            {
                PlayerChoice = player.CurrentChoice,
                OpponentChoice = opponent.CurrentChoice,
                Result = result,
                PlayerScore = player.Score,
                OpponentScore = opponent.Score,
                OpponentName = opponent.PlayerName,
                IsGameOver = isGameOver,
                GameOverWinner = gameOverWinner
            };

            await player.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GameResult,
                Data = JsonConvert.SerializeObject(gameData)
            });
        }

        // Возвращает результат для player против opponent (Win/Lose/Draw)
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

        private string GetChoiceEmoji(Choice choice) => choice switch
        {
            Choice.Rock => "🪨",
            Choice.Paper => "📄",
            Choice.Scissors => "✂️",
            _ => "❓"
        };

        // Вызывается при разрыве соединения: чистит комнату и уведомляет оппонента
        private void DisconnectClient(ClientHandler handler)
        {
            handler.CancelAfkTimer();
            var opponent = handler.Opponent;
            string playerName = handler.PlayerName ?? "Игрок";

            lock (clients) clients.Remove(handler);

            CleanupRoom(handler);

            if (opponent != null)
            {
                opponent.CancelAfkTimer();
                opponent.WantsRematch = false;
                _ = opponent.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.OpponentLeft,
                    Data = $"{playerName} отключился"
                });
                opponent.Opponent = null;
                opponent.Score = 0;
            }

            OnLog?.Invoke($"❌ «{playerName}» отключился от сервера", Brushes.Red);

            try { handler.Client?.Close(); } catch { }

            UpdateStats();
        }

        // Пересчитывает и публикует статистику: онлайн, активные игры, ожидающие, счётчик партий
        private void UpdateStats()
        {
            int playersOnline;
            List<ClientHandler> snapshot;
            lock (clients) { snapshot = clients.ToList(); playersOnline = snapshot.Count; }

            int activeGames = gameRooms.Count(r => r.IsFull);
            int waiting = gameRooms.Count(r => !r.IsFull);

            OnStatsUpdate?.Invoke(playersOnline, activeGames, waiting, totalGamesPlayed);

            var playerList = new List<PlayerInfo>();
            foreach (var client in snapshot)
            {
                string status = "В лобби";
                string trophies = "";
                if (client.Opponent != null)
                {
                    var modeTag = client.RoomMode == GameMode.FirstTo5 ? " [до 5]" : " [∞]";
                    status = $"Играет с «{client.Opponent.PlayerName}»{modeTag}";
                    trophies = $" • {client.Score}:{client.Opponent.Score}";
                }
                else if (gameRooms.Any(r => r.HostName == client.PlayerName && !r.IsFull))
                {
                    var myRoom = gameRooms.First(r => r.HostName == client.PlayerName && !r.IsFull);
                    var modeTag = myRoom.Mode == GameMode.FirstTo5 ? " [до 5]" : " [∞]";
                    status = $"Ожидает соперника{modeTag}";
                }

                playerList.Add(new PlayerInfo
                {
                    PlayerName = client.PlayerName ?? "Неизвестно",
                    Status = status + trophies,
                    Score = client.Score
                });
            }

            OnPlayerListUpdate?.Invoke(playerList);
        }
    }
}
