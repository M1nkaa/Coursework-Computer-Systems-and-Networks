using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using RPS.Client.Services;
using RPS.Shared;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace RPS.Client.ViewModels
{
    // Главная ViewModel клиентского приложения.
    // Управляет состоянием UI: подключением, лобби, игровым процессом,
    // анимацией и реваншем. Всё взаимодействие с сервером идёт через NetworkService.
    public class MainViewModel : INotifyPropertyChanged
    {
        private NetworkService networkService;

        #region Properties

        private string playerName = "Player";
        public string PlayerName
        {
            get => playerName;
            set { playerName = value; OnPropertyChanged(); }
        }

        private string serverIP = "127.0.0.1";
        public string ServerIP
        {
            get => serverIP;
            set { serverIP = value; OnPropertyChanged(); }
        }

        private string serverPort = "5000";
        public string ServerPort
        {
            get => serverPort;
            set { serverPort = value; OnPropertyChanged(); }
        }

        private string statusMessage = "Не подключено";
        public string StatusMessage
        {
            get => statusMessage;
            set { statusMessage = value; OnPropertyChanged(); }
        }

        private string opponentName = "Ожидание...";
        public string OpponentName
        {
            get => opponentName;
            set { opponentName = value; OnPropertyChanged(); }
        }

        private int playerScore = 0;
        public int PlayerScore
        {
            get => playerScore;
            set { playerScore = value; OnPropertyChanged(); }
        }

        private int opponentScore = 0;
        public int OpponentScore
        {
            get => opponentScore;
            set { opponentScore = value; OnPropertyChanged(); }
        }

        private string resultMessage = "";
        public string ResultMessage
        {
            get => resultMessage;
            set { resultMessage = value; OnPropertyChanged(); }
        }

        private string resultColor = "#FFFFFF";
        public string ResultColor
        {
            get => resultColor;
            set { resultColor = value; OnPropertyChanged(); }
        }

        private bool isConnected = false;
        public bool IsConnected
        {
            get => isConnected;
            set
            {
                isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotConnected));
                OnPropertyChanged(nameof(ShowLobby));
            }
        }

        public bool IsNotConnected => !isConnected;

        private bool isInGame = false;
        public bool IsInGame
        {
            get => isInGame;
            set
            {
                isInGame = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanPlay));
                OnPropertyChanged(nameof(ShowLobby));
            }
        }

        public bool CanPlay => isConnected && isInGame && !showGameEndScreen;
        public bool ShowLobby => isConnected && !isInGame && !showGameEndScreen;

        private bool showGameEndScreen = false;
        public bool ShowGameEndScreen
        {
            get => showGameEndScreen;
            set
            {
                showGameEndScreen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowLobby));
                OnPropertyChanged(nameof(CanPlay));
            }
        }

        private string gameEndMessage = "";
        public string GameEndMessage
        {
            get => gameEndMessage;
            set { gameEndMessage = value; OnPropertyChanged(); }
        }

        private bool isHost = false;
        public bool IsHostPlayer => isHost;
        public bool IsNotHostPlayer => !isHost;

        private bool iLeavingGame = false;

        private bool hasPlayerMadeChoice = false;
        public bool HasPlayerMadeChoice
        {
            get => hasPlayerMadeChoice;
            set
            {
                hasPlayerMadeChoice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanMakeChoice));
                OnPropertyChanged(nameof(ShowAfkTimer));
            }
        }

        public bool CanMakeChoice => IsInGame && !HasPlayerMadeChoice && !ShowAnimation;

        private bool showAnimation = false;
        public bool ShowAnimation
        {
            get => showAnimation;
            set { showAnimation = value; OnPropertyChanged(); }
        }

        private string leftPlayerEmoji = "✊";
        public string LeftPlayerEmoji
        {
            get => leftPlayerEmoji;
            set { leftPlayerEmoji = value; OnPropertyChanged(); }
        }

        private string rightPlayerEmoji = "✊";
        public string RightPlayerEmoji
        {
            get => rightPlayerEmoji;
            set { rightPlayerEmoji = value; OnPropertyChanged(); }
        }

        private bool hideScoreboard = false;
        public bool HideScoreboard
        {
            get => hideScoreboard;
            set { hideScoreboard = value; OnPropertyChanged(); }
        }

        private bool isShaking = false;
        public bool IsShaking
        {
            get => isShaking;
            set { isShaking = value; OnPropertyChanged(); }
        }

        private bool animationTrigger = false;
        public bool AnimationTrigger
        {
            get => animationTrigger;
            set { animationTrigger = value; OnPropertyChanged(); }
        }

        // --- Таймер AFK ---
        private int afkTimerSeconds = 0;
        public int AfkTimerSeconds
        {
            get => afkTimerSeconds;
            set { afkTimerSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowAfkTimer)); }
        }

        public bool ShowAfkTimer => afkTimerSeconds > 0 && IsInGame && !HasPlayerMadeChoice;

        // --- Выбор режима игры ---
        private GameMode selectedGameMode = GameMode.Infinite;
        public GameMode SelectedGameMode
        {
            get => selectedGameMode;
            set { selectedGameMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsInfiniteMode)); OnPropertyChanged(nameof(IsFirstTo5Mode)); }
        }

        public bool IsInfiniteMode
        {
            get => selectedGameMode == GameMode.Infinite;
            set { if (value) SelectedGameMode = GameMode.Infinite; }
        }

        public bool IsFirstTo5Mode
        {
            get => selectedGameMode == GameMode.FirstTo5;
            set { if (value) SelectedGameMode = GameMode.FirstTo5; }
        }

        // Режим текущей игры (полученный от сервера)
        private GameMode currentGameMode = GameMode.Infinite;
        public string GameModeLabel => currentGameMode == GameMode.FirstTo5 ? "🏆 До 5 побед" : "∞ Бесконечный";
        public string ScoreGoalLabel => currentGameMode == GameMode.FirstTo5 ? "/ 5" : "";

        // --- Реванш ---
        // Показывать ли кнопку "Реванш" (режим FirstTo5, конец серии, не AFK-кик)
        private bool showRematchButton = false;
        public bool ShowRematchButton
        {
            get => showRematchButton;
            set { showRematchButton = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowExitOnlyButton)); }
        }
        // Показывать ли только "Выйти в лобби" (без реванша)
        public bool ShowExitOnlyButton => !showRematchButton;

        private bool iWantRematch = false;
        public bool IWantRematch
        {
            get => iWantRematch;
            set { iWantRematch = value; OnPropertyChanged(); OnPropertyChanged(nameof(RematchButtonText)); }
        }

        private string rematchCount = "";
        public string RematchCount
        {
            get => rematchCount;
            set { rematchCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(RematchButtonText)); }
        }

        public string RematchButtonText
        {
            get
            {
                if (!iWantRematch) return "🔄 Реванш";
                if (!string.IsNullOrEmpty(rematchCount)) return $"⏳ Готовы: {rematchCount}";
                return "⏳ Ожидание соперника...";
            }
        }
        public bool CanClickRematch => !iWantRematch;

        public ObservableCollection<GameRoomViewModel> AvailableGames { get; set; }

        private GameRoomViewModel selectedGame;
        public GameRoomViewModel SelectedGame
        {
            get => selectedGame;
            set { selectedGame = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand LeaveGameCommand { get; }
        public ICommand CreateGameCommand { get; }
        public ICommand RefreshGamesCommand { get; }
        public ICommand JoinGameCommand { get; }
        public ICommand RockCommand { get; }
        public ICommand PaperCommand { get; }
        public ICommand ScissorsCommand { get; }
        public ICommand CreateNewGameAfterEndCommand { get; }
        public ICommand ExitToLobbyCommand { get; }
        public ICommand RematchCommand { get; }

        #endregion

        #region Constructor

        // Инициализирует сервис, подписывается на его события и создаёт команды
        public MainViewModel()
        {
            networkService = new NetworkService();
            networkService.MessageReceived += OnMessageReceived;
            networkService.Disconnected += OnDisconnected;
            networkService.ConnectionError += OnConnectionError;

            AvailableGames = new ObservableCollection<GameRoomViewModel>();

            ConnectCommand = new AsyncRelayCommand(ConnectToServer, () => !IsConnected);
            DisconnectCommand = new RelayCommand(DisconnectFromServer, () => IsConnected);
            LeaveGameCommand = new AsyncRelayCommand(LeaveGameAsync, () => IsInGame);
            CreateGameCommand = new AsyncRelayCommand(CreateGame, () => ShowLobby);
            RefreshGamesCommand = new AsyncRelayCommand(RefreshGames, () => ShowLobby);
            JoinGameCommand = new AsyncRelayCommand(JoinGame, () => ShowLobby && SelectedGame != null);
            RockCommand = new AsyncRelayCommand(() => MakeChoice(Choice.Rock), () => CanMakeChoice);
            PaperCommand = new AsyncRelayCommand(() => MakeChoice(Choice.Paper), () => CanMakeChoice);
            ScissorsCommand = new AsyncRelayCommand(() => MakeChoice(Choice.Scissors), () => CanMakeChoice);
            CreateNewGameAfterEndCommand = new AsyncRelayCommand(CreateNewGameAfterEnd);
            ExitToLobbyCommand = new RelayCommand(ExitToLobby);
            RematchCommand = new AsyncRelayCommand(RequestRematch, () => CanClickRematch);
        }

        #endregion

        #region Methods

        // Валидирует введённые данные и устанавливает TCP-соединение с сервером
        private async Task ConnectToServer()
        {
            string name = (PlayerName ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите ваше имя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (name.Length > 20)
            {
                MessageBox.Show("Имя не должно превышать 20 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ip = (ServerIP ?? "").Trim();
            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("Введите IP-адрес сервера!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ServerPort, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Введите корректный порт (1–65535)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusMessage = "Подключение...";
            bool success = await networkService.ConnectAsync(ip, port, name);

            if (success)
            {
                IsConnected = true;
                StatusMessage = "✅ Подключено к серверу";
                ResultMessage = "Создайте игру или присоединитесь к существующей!";
                ResultColor = "#4CAF50";
                await RefreshGames();
            }
            else
            {
                StatusMessage = "❌ Ошибка подключения";
            }
        }

        private void DisconnectFromServer()
        {
            networkService.Disconnect();
            ResetGameState();
            IsConnected = false;
            StatusMessage = "Отключено от сервера";
            ResultMessage = "";
            AvailableGames.Clear();
        }

        private async Task LeaveGameAsync()
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите выйти из игры?\nИгра будет завершена.",
                "Выход из игры", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                iLeavingGame = true;
                AfkTimerSeconds = 0;

                await networkService.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.LeaveGame,
                    PlayerName = PlayerName
                });

                ResetGameState();
                StatusMessage = "Вы вышли из игры";
                await RefreshGames();

                await Task.Delay(1000);
                iLeavingGame = false;
            }
        }

        private async Task CreateGame()
        {
            isHost = true;
            StatusMessage = "Создание игры...";

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.CreateGame,
                PlayerName = PlayerName,
                Data = SelectedGameMode.ToString()
            });
        }

        private async Task RefreshGames()
        {
            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.GetGamesList,
                PlayerName = PlayerName
            });
        }

        private async Task JoinGame()
        {
            if (SelectedGame == null) return;

            isHost = false;
            StatusMessage = $"Присоединение к игре {SelectedGame.HostName}...";

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.JoinGame,
                PlayerName = PlayerName,
                Data = SelectedGame.RoomId
            });
        }

        // Отправляет выбор игрока на сервер и блокирует повторный ход
        private async Task MakeChoice(Choice choice)
        {
            HasPlayerMadeChoice = true;
            AfkTimerSeconds = 0; // скрываем таймер

            ResultMessage = $"Вы выбрали: {GetChoiceText(choice)}\n\n⏳ Ожидание хода соперника...";
            ResultColor = "#FFA726";

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.MakeChoice,
                PlayerName = PlayerName,
                Data = choice.ToString()
            });
        }

        private async Task RequestRematch()
        {
            IWantRematch = true;
            OnPropertyChanged(nameof(RematchButtonText));
            OnPropertyChanged(nameof(CanClickRematch));
            CommandManager.InvalidateRequerySuggested();

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.RematchRequest,
                PlayerName = PlayerName
            });
        }

        private async Task CreateNewGameAfterEnd()
        {
            ShowGameEndScreen = false;
            ResetGameState();

            isHost = true;
            StatusMessage = "Создание игры...";

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.CreateGame,
                PlayerName = PlayerName,
                Data = SelectedGameMode.ToString()
            });
        }

        private void ExitToLobby()
        {
            iLeavingGame = true;
            // Уведомляем сервер (нужно на случай ожидания реванша)
            _ = networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.LeaveGame,
                PlayerName = PlayerName
            });
            ShowGameEndScreen = false;
            ResetGameState();
            StatusMessage = "В лобби";
            _ = RefreshGames();
            // Сбрасываем флаг с задержкой
            _ = Task.Delay(500).ContinueWith(_ => iLeavingGame = false);
        }

        // Диспетчер входящих сообщений от сервера — всегда исполняется в UI-потоке
        private void OnMessageReceived(NetworkMessage message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (message.Type)
                {
                    case MessageType.Connect:
                        break;

                    case MessageType.CreateGame:
                        IsInGame = true;
                        currentGameMode = GameMode.Infinite; // хост знает режим сам
                        StatusMessage = "Игра создана! Ожидание соперника...";
                        ResultMessage = "🎮 Ваша игра создана!\n\n⏳ Ожидание соперника...";
                        ResultColor = "#CDDC39";
                        OpponentName = "Ожидание...";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        AfkTimerSeconds = 0;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        OnPropertyChanged(nameof(GameModeLabel));
                        OnPropertyChanged(nameof(ScoreGoalLabel));
                        break;

                    case MessageType.GamesList:
                        UpdateGamesList(message.Data);
                        break;

                    case MessageType.GameStarted:
                    {
                        // Data = "GuestName|Mode"
                        var parts = message.Data.Split('|');
                        OpponentName = parts[0];
                        if (parts.Length > 1 && Enum.TryParse<GameMode>(parts[1], out var mode))
                        {
                            currentGameMode = mode;
                            SelectedGameMode = mode;
                        }
                        ShowGameEndScreen = false;
                        IWantRematch = false;
                        ShowRematchButton = false;
                        RematchCount = "";
                        IsInGame = true;
                        StatusMessage = $"🎮 Играете с: {OpponentName}";
                        ResultMessage = $"✅ {OpponentName} присоединился!\n\n👇 Выберите фигуру для начала игры";
                        ResultColor = "#4CAF50";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        AfkTimerSeconds = 0;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        OnPropertyChanged(nameof(GameModeLabel));
                        OnPropertyChanged(nameof(ScoreGoalLabel));
                        break;
                    }

                    case MessageType.GameJoined:
                    {
                        var parts = message.Data.Split('|');
                        OpponentName = parts[0];
                        if (parts.Length > 1 && Enum.TryParse<GameMode>(parts[1], out var mode))
                        {
                            currentGameMode = mode;
                            SelectedGameMode = mode;
                        }
                        ShowGameEndScreen = false;
                        IWantRematch = false;
                        ShowRematchButton = false;
                        RematchCount = "";
                        IsInGame = true;
                        StatusMessage = $"🎮 Играете с: {OpponentName}";
                        ResultMessage = "✅ Игра началась!\n\n👇 Выберите фигуру";
                        ResultColor = "#4CAF50";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        AfkTimerSeconds = 0;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        OnPropertyChanged(nameof(GameModeLabel));
                        OnPropertyChanged(nameof(ScoreGoalLabel));
                        break;
                    }

                    case MessageType.GameFull:
                        MessageBox.Show("Эта игра уже заполнена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _ = RefreshGames();
                        break;

                    case MessageType.GameResult:
                        ProcessGameResult(message.Data);
                        break;

                    case MessageType.OpponentLeft:
                        HandleOpponentLeft(message.Data);
                        break;

                    case MessageType.OpponentMadeChoice:
                        if (!HasPlayerMadeChoice)
                        {
                            ResultMessage = "⚡ Соперник уже сделал выбор!\n\n👇 Сделайте свой ход!";
                            ResultColor = "#FF9800";
                        }
                        break;

                    case MessageType.TimerUpdate:
                        if (int.TryParse(message.Data, out int secs))
                        {
                            AfkTimerSeconds = secs;
                            OnPropertyChanged(nameof(ShowAfkTimer));
                        }
                        break;

                    case MessageType.PlayerAfk:
                        // Нас кикнули за AFK — возвращаемся в лобби, НЕ отключаемся
                        AfkTimerSeconds = 0;
                        ResetGameState();
                        IsConnected = true; // остаёмся подключёнными
                        IsInGame = false;
                        ShowGameEndScreen = false;
                        MessageBox.Show("Вы были отключены от игры за бездействие (AFK).\nСлишком долго не делали ход!", 
                            "⏱️ AFK-кик", MessageBoxButton.OK, MessageBoxImage.Warning);
                        StatusMessage = "Вы были кикнуты за AFK";
                        _ = RefreshGames();
                        break;

                    case MessageType.OpponentAfk:
                        // Оппонент кикнут за AFK — нам засчитывается победа
                        AfkTimerSeconds = 0;
                        string afkOpponentName = OpponentName;
                        bool hadScore = PlayerScore > 0 || OpponentScore > 0;
                        ShowRematchButton = false;
                        IWantRematch = false;
                        RematchCount = "";
                        IsInGame = false;
                        ShowGameEndScreen = true;
                        GameEndMessage = $"⏱️ {afkOpponentName} был отключён за бездействие (AFK).\n\n🏆 Вы победили!";
                        OnPropertyChanged(nameof(IsHostPlayer));
                        OnPropertyChanged(nameof(IsNotHostPlayer));
                        _ = RefreshGames();
                        break;

                    case MessageType.Error:
                        MessageBox.Show(message.Data, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case MessageType.RematchRequest:
                        // message.Data = "1/2" или "2/2" — счётчик готовности
                        RematchCount = message.Data ?? "";
                        OnPropertyChanged(nameof(RematchButtonText));
                        break;

                    case MessageType.RematchAccepted:
                        // Сервер ответит через GameStarted/GameJoined — ничего не делаем здесь
                        break;

                    case MessageType.RematchDeclined:
                        IWantRematch = false;
                        ShowRematchButton = false;
                        MessageBox.Show(message.Data, "Реванш недоступен", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                }
            });
        }

        private void HandleOpponentLeft(string msg)
        {
            if (iLeavingGame) return;

            AfkTimerSeconds = 0;
            ShowRematchButton = false;
            IWantRematch = false;
            RematchCount = "";
            bool isAfkKick = msg.Contains("AFK") || msg.Contains("отключён за");

            ShowGameEndScreen = true;
            IsInGame = false;

            string winner = PlayerScore > OpponentScore ? "🏆 Вы победили!" :
                            PlayerScore < OpponentScore ? $"😢 Победил {OpponentName}!" : "🤝 Ничья!";

            string reason = isAfkKick
                ? $"⏱️ {OpponentName} был отключён за бездействие (AFK)."
                : $"{OpponentName} покинул игру.";

            GameEndMessage = $"{reason}\n\nИтоговый счёт: {PlayerScore} — {OpponentScore}\n{winner}";

            OnPropertyChanged(nameof(IsHostPlayer));
            OnPropertyChanged(nameof(IsNotHostPlayer));
        }

        private void UpdateGamesList(string data)
        {
            try
            {
                var rooms = JsonConvert.DeserializeObject<List<GameRoom>>(data);
                AvailableGames.Clear();
                foreach (var room in rooms)
                {
                    AvailableGames.Add(new GameRoomViewModel
                    {
                        RoomId = room.RoomId,
                        HostName = room.HostName,
                        Mode = room.Mode,
                        DisplayName = room.GetDisplayName()
                    });
                }
                StatusMessage = $"Найдено игр: {AvailableGames.Count}";
            }
            catch { }
        }

        // Обрабатывает результат раунда: воспроизводит анимацию,
        // обновляет счёт и при необходимости показывает экран конца серии
        private async void ProcessGameResult(string data)
        {
            try
            {
                GameData gameData = JsonConvert.DeserializeObject<GameData>(data);

                AfkTimerSeconds = 0;

                await PlayRockPaperScissorsAnimation(gameData.PlayerChoice, gameData.OpponentChoice,
                    sendAnimationDone: !gameData.IsGameOver);

                PlayerScore = gameData.PlayerScore;
                OpponentScore = gameData.OpponentScore;

                string playerChoice = GetChoiceText(gameData.PlayerChoice);
                string opponentChoice = GetChoiceText(gameData.OpponentChoice);

                // Проверяем конец серии (FirstTo5)
                if (gameData.IsGameOver)
                {
                    bool iWon = gameData.GameOverWinner == PlayerName;
                    string seriesResult = iWon ? "🏆 Серия завершена — ВЫ ПОБЕДИЛИ!" : $"😢 Серия завершена — победил {gameData.GameOverWinner}!";

                    switch (gameData.Result)
                    {
                        case GameResult.Win:
                            ResultMessage = $"🎉 ПОБЕДА в раунде!\n{playerChoice} побеждает {opponentChoice}";
                            ResultColor = "#4CAF50";
                            break;
                        case GameResult.Lose:
                            ResultMessage = $"😢 Поражение в раунде!\n{opponentChoice} побеждает {playerChoice}";
                            ResultColor = "#F44336";
                            break;
                        case GameResult.Draw:
                            ResultMessage = $"🤝 Ничья в раунде!\nОба выбрали {playerChoice}";
                            ResultColor = "#FFC107";
                            break;
                    }

                    // Небольшая задержка, потом показываем экран конца
                    await Task.Delay(1500);

                    ShowGameEndScreen = true;
                    IsInGame = false;
                    ShowRematchButton = true;
                    IWantRematch = false;
                    GameEndMessage = $"{seriesResult}\n\nФинальный счёт: {PlayerScore} — {OpponentScore}";

                    HasPlayerMadeChoice = false;
                    OnPropertyChanged(nameof(CanMakeChoice));
                    OnPropertyChanged(nameof(IsHostPlayer));
                    OnPropertyChanged(nameof(IsNotHostPlayer));
                    return;
                }

                // Обычный раунд
                switch (gameData.Result)
                {
                    case GameResult.Win:
                        ResultMessage = $"🎉 ПОБЕДА!\n{playerChoice} побеждает {opponentChoice}\n\n👇 Выберите фигуру для следующего раунда";
                        ResultColor = "#4CAF50";
                        break;
                    case GameResult.Lose:
                        ResultMessage = $"😢 Поражение!\n{opponentChoice} побеждает {playerChoice}\n\n👇 Выберите фигуру для следующего раунда";
                        ResultColor = "#F44336";
                        break;
                    case GameResult.Draw:
                        ResultMessage = $"🤝 Ничья!\nОба выбрали {playerChoice}\n\n👇 Выберите фигуру для следующего раунда";
                        ResultColor = "#FFC107";
                        break;
                }

                // Разблокируем кнопки сразу
                HasPlayerMadeChoice = false;
                OnPropertyChanged(nameof(CanMakeChoice));
                // Принудительно обновляем все команды
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки результата: {ex.Message}");
                HasPlayerMadeChoice = false;
                OnPropertyChanged(nameof(CanMakeChoice));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Воспроизводит анимацию «камень-ножницы-бумага»: сначала оба кулака,
        // потом финальные фигуры; после окончания уведомляет сервер
        private async Task PlayRockPaperScissorsAnimation(Choice playerChoice, Choice opponentChoice, bool sendAnimationDone = true)
        {
            ShowAnimation = true;
            HideScoreboard = true;

            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";

            AnimationTrigger = false;
            await Task.Delay(50);
            AnimationTrigger = true;

            await Task.Delay(1600);

            LeftPlayerEmoji = GetChoiceEmoji(playerChoice);
            RightPlayerEmoji = GetChoiceEmoji(opponentChoice);

            await Task.Delay(2500);

            ShowAnimation = false;
            HideScoreboard = false;
            AnimationTrigger = false;

            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";

            // Сообщаем серверу что анимация завершена — можно запускать AFK-таймер
            if (sendAnimationDone)
            {
                await networkService.SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.AnimationDone,
                    PlayerName = PlayerName
                });
            }
        }

        private string GetChoiceEmoji(Choice choice) => choice switch
        {
            Choice.Rock => "🤜",
            Choice.Paper => "✋",
            Choice.Scissors => "✌️",
            _ => "❓"
        };

        private void OnDisconnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AfkTimerSeconds = 0;
                ResetGameState();
                IsConnected = false;
                StatusMessage = "❌ Соединение потеряно";
                ResultMessage = "Отключено от сервера";
                ResultColor = "#F44336";
                AvailableGames.Clear();
            });
        }

        private void OnConnectionError(string error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        // Сбрасывает все игровые поля в начальное состояние (например, при выходе в лобби)
        private void ResetGameState()
        {
            IsInGame = false;
            ShowGameEndScreen = false;
            PlayerScore = 0;
            OpponentScore = 0;
            OpponentName = "Ожидание...";
            ResultMessage = "";
            isHost = false;
            HasPlayerMadeChoice = false;
            ShowAnimation = false;
            HideScoreboard = false;
            IsShaking = false;
            AnimationTrigger = false;
            AfkTimerSeconds = 0;
            ShowRematchButton = false;
            IWantRematch = false;
            RematchCount = "";
            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";
            OnPropertyChanged(nameof(IsHostPlayer));
            OnPropertyChanged(nameof(IsNotHostPlayer));
            OnPropertyChanged(nameof(CanMakeChoice));
            OnPropertyChanged(nameof(ShowAfkTimer));
        }

        private string GetChoiceText(Choice choice) => choice switch
        {
            Choice.Rock => "🤜 Камень",
            Choice.Paper => "✋ Бумага",
            Choice.Scissors => "✌️ Ножницы",
            _ => ""
        };

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

    // Простая ViewModel для отображения одной комнаты в списке лобби
    public class GameRoomViewModel
    {
        public string RoomId { get; set; }
        public string HostName { get; set; }
        public string DisplayName { get; set; }
        public GameMode Mode { get; set; }
    }
}
