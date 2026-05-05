using System;

namespace RPS.Shared
{
    // Информация об игровой комнате
    [Serializable]
    public class GameRoom
    {
        public string RoomId { get; set; }           // Уникальный ID комнаты
        public string HostName { get; set; }         // Имя создателя
        public string GuestName { get; set; }        // Имя гостя (если есть)
        public bool IsFull { get; set; }             // Комната заполнена?
        public DateTime Created { get; set; }        // Время создания

        public GameRoom()
        {
            RoomId = Guid.NewGuid().ToString();
            Created = DateTime.Now;
            IsFull = false;
        }

        public string GetDisplayName()
        {
            if (IsFull)
                return $"🔴 {HostName} vs {GuestName} (Играют)";
            else
                return $"🟢 Игра {HostName} (Ожидает игрока)";
        }
    }
}