using Newtonsoft.Json;
using System;

namespace RPS.Shared
{
    // Универсальный пакет данных, который клиент и сервер гоняют друг другу по TCP.
    // Data — строка с доп. полезной нагрузкой (обычно JSON или простое значение).
    [Serializable]
    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public string PlayerName { get; set; }
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }

        public NetworkMessage()
        {
            Timestamp = DateTime.Now;
        }

        // Сериализация в JSON для отправки по сети
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        // Десериализация входящего JSON-пакета
        public static NetworkMessage FromJson(string json)
        {
            return JsonConvert.DeserializeObject<NetworkMessage>(json);
        }
    }
}
