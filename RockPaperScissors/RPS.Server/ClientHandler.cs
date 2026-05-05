using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using RPS.Shared;

namespace RPS.Server
{
    public class ClientHandler
    {
        public TcpClient Client { get; set; }
        public string PlayerName { get; set; }
        public NetworkStream Stream { get; set; }
        public ClientHandler Opponent { get; set; }
        public Choice CurrentChoice { get; set; }
        public int Score { get; set; }
        public string RoomId { get; set; } // НОВОЕ!

        public ClientHandler(TcpClient client)
        {
            Client = client;
            Stream = client.GetStream();
            CurrentChoice = Choice.None;
            Score = 0;
        }

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
    }
}