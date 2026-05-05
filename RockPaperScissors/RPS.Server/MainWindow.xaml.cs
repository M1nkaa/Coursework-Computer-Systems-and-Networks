using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace RPS.Server
{
    public partial class MainWindow : Window
    {
        private GameServer gameServer;
        private bool isServerRunning = false;

        // Коллекция для списка игроков
        public ObservableCollection<PlayerInfo> Players { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            Players = new ObservableCollection<PlayerInfo>();
            PlayersListView.ItemsSource = Players;

            gameServer = new GameServer();
            gameServer.OnLog += AddLog;
            gameServer.OnStatsUpdate += UpdateStats;
            gameServer.OnPlayerListUpdate += UpdatePlayerList; // НОВОЕ!

            AddLog("✅ Сервер инициализирован", Brushes.LightGreen);
        }

        // Запуск/остановка сервера
        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isServerRunning)
            {
                // ЗАПУСК
                if (!int.TryParse(PortTextBox.Text, out int port))
                {
                    MessageBox.Show("Введите корректный номер порта!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                isServerRunning = true;
                StartStopButton.Content = "⏹️ ОСТАНОВИТЬ";
                StatusText.Text = $"Сервер работает на порту {port}";
                PortTextBox.IsEnabled = false;

                AddLog($"🚀 Запуск сервера на порту {port}...", Brushes.Cyan);

                _ = gameServer.StartAsync(port);
            }
            else
            {
                // ОСТАНОВКА
                isServerRunning = false;
                StartStopButton.Content = "▶️ ЗАПУСТИТЬ";
                StatusText.Text = "Сервер остановлен";
                PortTextBox.IsEnabled = true;

                AddLog("⏹️ Сервер остановлен", Brushes.Orange);

                gameServer.Stop();
                Players.Clear();
                UpdateStats(0, 0, 0, 0);
            }
        }

        // Добавление записи в лог
        private void AddLog(string message, Brush color = null)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}\n";

                LogTextBlock.Text += logEntry;
                LogScrollViewer.ScrollToEnd();
            });
        }

        // Обновление статистики
        private void UpdateStats(int playersOnline, int activeGames, int waiting, int totalGames)
        {
            Dispatcher.Invoke(() =>
            {
                PlayersOnlineText.Text = playersOnline.ToString();
                ActiveGamesText.Text = activeGames.ToString();
                WaitingPlayersText.Text = waiting.ToString();
                TotalGamesText.Text = totalGames.ToString();
            });
        }

        // НОВОЕ: Обновление списка игроков
        private void UpdatePlayerList(System.Collections.Generic.List<PlayerInfo> players)
        {
            Dispatcher.Invoke(() =>
            {
                Players.Clear();
                foreach (var player in players)
                {
                    Players.Add(player);
                }
            });
        }

        // Очистка лога
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = "";
            AddLog("🗑️ Журнал очищен", Brushes.Gray);
        }

        // Закрытие окна
        protected override void OnClosed(EventArgs e)
        {
            gameServer?.Stop();
            base.OnClosed(e);
        }
    }

    // Класс для отображения информации об игроке
    public class PlayerInfo
    {
        public string PlayerName { get; set; }
        public string Status { get; set; }
        public int Score { get; set; }
    }
}