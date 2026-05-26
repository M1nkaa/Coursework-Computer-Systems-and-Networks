using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RPS.Shared;

namespace RPS.Server
{
    // Хранит состояние одного подключённого клиента и умеет
    // отправлять/получать сообщения, а также управлять AFK-таймером
    public class ClientHandler
    {
        public TcpClient Client { get; set; }
        public string PlayerName { get; set; }
        public NetworkStream Stream { get; set; }
        public ClientHandler Opponent { get; set; }   // null, если не в игре
        public Choice CurrentChoice { get; set; }
        public int Score { get; set; }
        public string RoomId { get; set; }
        public GameMode RoomMode { get; set; }
        public bool WantsRematch { get; set; }
        public bool AnimationReady { get; set; }  // клиент закончил анимацию

        // AFK-таймер
        private CancellationTokenSource _afkCts;

        public ClientHandler(TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
            CurrentChoice = Choice.None;
            Score = 0;
        }

        // Отправляет сообщение клиенту; ошибки сети тихо проглатываются,
        // чтобы не ронять сервер из-за одного сломанного соединения
        public async Task SendMessageAsync(NetworkMessage message)
        {
            try
            {
                string json = message.ToJson();
                byte[] data = Encoding.UTF8.GetBytes(json + "\n");
                await Stream.WriteAsync(data, 0, data.Length);
            }
            catch { }
        }

        // Читает одно сообщение из потока; возвращает null при разрыве соединения
        public async Task<NetworkMessage> ReceiveMessageAsync()
        {
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead = await Stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) return null;
                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                return NetworkMessage.FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Запускает 30-секундный AFK-таймер. Каждую секунду вызывает onTick(secondsLeft),
        /// по истечению — onTimeout().
        /// </summary>
        public void StartAfkTimer(Func<int, Task> onTick, Func<Task> onTimeout)
        {
            CancelAfkTimer();
            _afkCts = new CancellationTokenSource();
            var token = _afkCts.Token;

            _ = Task.Run(async () =>
            {
                for (int i = 30; i >= 1; i--)
                {
                    if (token.IsCancellationRequested) return;
                    await onTick(i);
                    await Task.Delay(1000, token).ContinueWith(_ => { }); // не бросать при отмене
                }
                if (!token.IsCancellationRequested)
                    await onTimeout();
            });
        }

        // Отменяет текущий AFK-таймер (например, когда игрок сделал ход)
        public void CancelAfkTimer()
        {
            _afkCts?.Cancel();
            _afkCts = null;
        }
    }
}
