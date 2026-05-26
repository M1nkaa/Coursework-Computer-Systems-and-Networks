using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Media;

namespace RPS.Server
{
    public partial class MainWindow : Window
    {
        private GameServer gameServer;
        private bool isServerRunning = false;

        public ObservableCollection<PlayerInfo> Players { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            Players = new ObservableCollection<PlayerInfo>();
            PlayersListView.ItemsSource = Players;

            gameServer = new GameServer();
            gameServer.OnLog += AddLog;
            gameServer.OnStatsUpdate += UpdateStats;
            gameServer.OnPlayerListUpdate += UpdatePlayerList;

            AddLog("✅ Сервер инициализирован. Введите порт и нажмите «ЗАПУСТИТЬ».", Brushes.LightGreen);
        }

        private async void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isServerRunning)
            {
                if (!int.TryParse(PortTextBox.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Введите корректный номер порта (1–65535)!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                isServerRunning = true;
                StartStopButton.Content = "⏹️ ОСТАНОВИТЬ";
                StatusText.Text = $"Сервер работает на порту {port}";
                PortTextBox.IsEnabled = false;

                string ipAddress = GetLocalIPAddress();
                ServerIpText.Text = ipAddress;

                AddLog($"🚀 Запуск на {ipAddress}:{port}…", Brushes.Cyan);
                _ = gameServer.StartAsync(port);
            }
            else
            {
                isServerRunning = false;
                StartStopButton.Content = "▶️ ЗАПУСТИТЬ";
                StatusText.Text = "Сервер остановлен";
                PortTextBox.IsEnabled = true;
                ServerIpText.Text = "Не определён";

                AddLog("⏹️ Сервер остановлен.", Brushes.Orange);
                gameServer.Stop();
                Players.Clear();
                UpdateStats(0, 0, 0, 0);
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
                return "127.0.0.1";
            }
            catch { return "Ошибка определения IP"; }
        }

        private void AddLog(string message, Brush color = null)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogTextBlock.Text += $"[{timestamp}] {message}\n";
                LogScrollViewer.ScrollToEnd();
            });
        }

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

        private void UpdatePlayerList(List<PlayerInfo> players)
        {
            Dispatcher.Invoke(() =>
            {
                Players.Clear();
                foreach (var p in players)
                    Players.Add(p);
            });
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = "";
            AddLog("🗑️ Журнал очищен.", Brushes.Gray);
        }

        protected override void OnClosed(EventArgs e)
        {
            gameServer?.Stop();
            base.OnClosed(e);
        }
    }

    public class PlayerInfo
    {
        public string PlayerName { get; set; }
        public string Status { get; set; }
        public int Score { get; set; }
    }
}
