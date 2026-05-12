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
    public class MainViewModel : INotifyPropertyChanged
    {
        private NetworkService networkService;

        #region Properties (Свойства)

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

        // Флаг "я уже сделал выбор"
        private bool hasPlayerMadeChoice = false;
        public bool HasPlayerMadeChoice
        {
            get => hasPlayerMadeChoice;
            set
            {
                hasPlayerMadeChoice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanMakeChoice));
            }
        }

        // Можно ли делать выбор
        public bool CanMakeChoice => IsInGame && !HasPlayerMadeChoice && !ShowAnimation;

        // Показываем анимацию
        private bool showAnimation = false;
        public bool ShowAnimation
        {
            get => showAnimation;
            set { showAnimation = value; OnPropertyChanged(); }
        }

        // Эмодзи для анимации (два кулачка)
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

        // Скрывать табло во время анимации
        private bool hideScoreboard = false;
        public bool HideScoreboard
        {
            get => hideScoreboard;
            set { hideScoreboard = value; OnPropertyChanged(); }
        }

        // НОВОЕ: Для анимации тряски
        private bool isShaking = false;
        public bool IsShaking
        {
            get => isShaking;
            set { isShaking = value; OnPropertyChanged(); }
        }

        public ObservableCollection<GameRoomViewModel> AvailableGames { get; set; }

        private GameRoomViewModel selectedGame;
        public GameRoomViewModel SelectedGame
        {
            get => selectedGame;
            set { selectedGame = value; OnPropertyChanged(); }
        }

        // НОВОЕ: Триггер для запуска анимации
        private bool animationTrigger = false;
        public bool AnimationTrigger
        {
            get => animationTrigger;
            set { animationTrigger = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands (Команды)

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

        #endregion

        #region Constructor (Конструктор)

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
        }

        #endregion

        #region Methods (Методы)

        private async Task ConnectToServer()
        {
            if (string.IsNullOrWhiteSpace(PlayerName))
            {
                MessageBox.Show("Введите ваше имя!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(ServerPort, out int port))
            {
                MessageBox.Show("Введите корректный порт!", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusMessage = "Подключение...";
            bool success = await networkService.ConnectAsync(ServerIP, port, PlayerName);

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
                "Выход из игры",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                iLeavingGame = true;

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
                PlayerName = PlayerName
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

        private async Task MakeChoice(Choice choice)
        {
            HasPlayerMadeChoice = true;

            ResultMessage = $"Вы выбрали: {GetChoiceText(choice)}\n\n⏳ Ожидание хода соперника...";
            ResultColor = "#FFA726";

            await networkService.SendMessageAsync(new NetworkMessage
            {
                Type = MessageType.MakeChoice,
                PlayerName = PlayerName,
                Data = choice.ToString()
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
                PlayerName = PlayerName
            });
        }

        private void ExitToLobby()
        {
            ShowGameEndScreen = false;
            ResetGameState();
            StatusMessage = "В лобби";
            _ = RefreshGames();
        }

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
                        StatusMessage = "Игра создана! Ожидание соперника...";
                        ResultMessage = "🎮 Ваша игра создана!\n\n⏳ Ожидание соперника...";
                        ResultColor = "#CDDC39";
                        OpponentName = "Ожидание...";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        break;

                    case MessageType.GamesList:
                        UpdateGamesList(message.Data);
                        break;

                    case MessageType.GameStarted:
                        OpponentName = message.Data;
                        IsInGame = true;
                        StatusMessage = $"🎮 Играете с: {OpponentName}";
                        ResultMessage = "✅ Соперник присоединился!\n\n👇 Выберите фигуру для начала игры";
                        ResultColor = "#4CAF50";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        break;

                    case MessageType.GameJoined:
                        OpponentName = message.Data;
                        IsInGame = true;
                        StatusMessage = $"🎮 Играете с: {OpponentName}";
                        ResultMessage = "✅ Игра началась!\n\n👇 Выберите фигуру";
                        ResultColor = "#4CAF50";
                        PlayerScore = 0;
                        OpponentScore = 0;
                        HasPlayerMadeChoice = false;
                        OnPropertyChanged(nameof(CanMakeChoice));
                        break;

                    case MessageType.GameFull:
                        MessageBox.Show("Эта игра уже заполнена!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    case MessageType.Error:
                        MessageBox.Show(message.Data, "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            });
        }

        private void HandleOpponentLeft(string message)
        {
            if (iLeavingGame)
            {
                return;
            }

            ShowGameEndScreen = true;
            IsInGame = false;

            string winner = PlayerScore > OpponentScore ? "Вы победили!" :
                           PlayerScore < OpponentScore ? $"{OpponentName} победил!" : "Ничья!";

            if (isHost)
            {
                GameEndMessage = $"{OpponentName} покинул игру.\n\n" +
                                $"Итоговый счёт: {PlayerScore} - {OpponentScore}\n" +
                                $"{winner}";
            }
            else
            {
                GameEndMessage = $"{OpponentName} (хост) покинул игру.\n\n" +
                                $"Итоговый счёт: {PlayerScore} - {OpponentScore}\n" +
                                $"{winner}";
            }

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
                        DisplayName = room.GetDisplayName()
                    });
                }

                StatusMessage = $"Найдено игр: {AvailableGames.Count}";
            }
            catch { }
        }

        private async void ProcessGameResult(string data)
        {
            try
            {
                GameData gameData = JsonConvert.DeserializeObject<GameData>(data);

                // Запускаем анимацию
                await PlayRockPaperScissorsAnimation(gameData.PlayerChoice, gameData.OpponentChoice);

                // Обновляем счёт
                PlayerScore = gameData.PlayerScore;
                OpponentScore = gameData.OpponentScore;

                string playerChoice = GetChoiceText(gameData.PlayerChoice);
                string opponentChoice = GetChoiceText(gameData.OpponentChoice);

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

                // Разблокируем кнопки
                HasPlayerMadeChoice = false;
                OnPropertyChanged(nameof(CanMakeChoice)); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки результата: {ex.Message}");
                HasPlayerMadeChoice = false;
                OnPropertyChanged(nameof(CanMakeChoice)); 
            }
        }

        private async Task PlayRockPaperScissorsAnimation(Choice playerChoice, Choice opponentChoice)
        {
            ShowAnimation = true;
            HideScoreboard = true;

            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";

            // Запускаем анимацию через триггер
            AnimationTrigger = false;
            await Task.Delay(50); // Небольшая задержка
            AnimationTrigger = true;

            // Ждём пока анимация тряски отыграет (3 раза по 0.5 сек = 1.5 сек)
            await Task.Delay(1600);

            // Показываем выбранные фигуры
            LeftPlayerEmoji = GetChoiceEmoji(playerChoice);
            RightPlayerEmoji = GetChoiceEmoji(opponentChoice);

            // Держим результат 2.5 секунды
            await Task.Delay(2500);

            ShowAnimation = false;
            HideScoreboard = false;
            AnimationTrigger = false;

            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";
        }

        // Получить эмодзи для выбора
        private string GetChoiceEmoji(Choice choice)
        {
            return choice switch
            {
                Choice.Rock => "✊",
                Choice.Paper => "✋",
                Choice.Scissors => "✌️",
                _ => "❓"
            };
        }

        private void OnDisconnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
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
            LeftPlayerEmoji = "✊";
            RightPlayerEmoji = "✊";
            OnPropertyChanged(nameof(IsHostPlayer));
            OnPropertyChanged(nameof(IsNotHostPlayer));
            OnPropertyChanged(nameof(CanMakeChoice));
        }

        private string GetChoiceText(Choice choice)
        {
            return choice switch
            {
                Choice.Rock => "🪨 Камень",
                Choice.Paper => "📄 Бумага",
                Choice.Scissors => "✂️ Ножницы",
                _ => ""
            };
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }

    public class GameRoomViewModel
    {
        public string RoomId { get; set; }
        public string HostName { get; set; }
        public string DisplayName { get; set; }
    }
}