using Newtonsoft.Json;
using System;

namespace RPS.Shared
{
    // Класс для сообщений между клиентом и сервером
    [Serializable]
    public class NetworkMessage
    {
        // Тип сообщения (подключение, выбор и т.д.)
        public MessageType Type { get; set; }

        // Имя игрока
        public string PlayerName { get; set; }

        // Данные сообщения (может быть JSON)
        public string Data { get; set; }

        // Время отправки
        public DateTime Timestamp { get; set; }

        // Конструктор - вызывается при создании объекта
        public NetworkMessage()
        {
            Timestamp = DateTime.Now;
        }

        // Преобразовать объект в JSON строку
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        // Создать объект из JSON строки
        public static NetworkMessage FromJson(string json)
        {
            return JsonConvert.DeserializeObject<NetworkMessage>(json);
        }
    }
}