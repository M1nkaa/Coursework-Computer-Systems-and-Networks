using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RPS.Shared;

namespace RPS.Client.Services
{
    public class NetworkService
    {
        private TcpClient client;
        private NetworkStream stream;
        private bool isConnected;

        // События для уведомления UI
        public event Action<NetworkMessage> MessageReceived;
        public event Action Disconnected;
        public event Action<string> ConnectionError;

        // Подключение к серверу
        public async Task<bool> ConnectAsync(string host, int port, string playerName)
        {
            try
            {
                client = new TcpClient();
                await client.ConnectAsync(host, port);
                stream = client.GetStream();
                isConnected = true;

                // Отправляем имя игрока
                await SendMessageAsync(new NetworkMessage
                {
                    Type = MessageType.Connect,
                    PlayerName = playerName
                });

                // Запускаем прослушивание сообщений
                _ = Task.Run(ListenForMessagesAsync);

                return true;
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        // Отправка сообщения на сервер
        public async Task SendMessageAsync(NetworkMessage message)
        {
            if (!isConnected || stream == null) return;

            try
            {
                string json = message.ToJson();
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                ConnectionError?.Invoke($"Ошибка отправки: {ex.Message}");
            }
        }

        // Прослушивание сообщений от сервера
        private async Task ListenForMessagesAsync()
        {
            byte[] buffer = new byte[4096];

            while (isConnected)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        Disconnect();
                        break;
                    }

                    string json = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    NetworkMessage message = NetworkMessage.FromJson(json);

                    // Уведомляем UI о новом сообщении
                    MessageReceived?.Invoke(message);
                }
                catch (Exception)
                {
                    Disconnect();
                    break;
                }
            }
        }

        // Отключение от сервера
        public void Disconnect()
        {
            isConnected = false;
            stream?.Close();
            client?.Close();
            Disconnected?.Invoke();
        }

        public bool IsConnected => isConnected;
    }
}