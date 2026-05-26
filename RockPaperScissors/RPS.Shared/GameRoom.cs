using System;

namespace RPS.Shared
{
    // Описание одной игровой комнаты — используется для списка доступных игр в лобби
    [Serializable]
    public class GameRoom
    {
        public string RoomId { get; set; }
        public string HostName { get; set; }
        public string GuestName { get; set; }
        public bool IsFull { get; set; }
        public DateTime Created { get; set; }
        public GameMode Mode { get; set; }  // Режим игры

        public GameRoom()
        {
            RoomId = Guid.NewGuid().ToString();
            Created = DateTime.Now;
            IsFull = false;
            Mode = GameMode.Infinite;
        }

        // Строка для отображения в списке лобби: статус, игроки, режим
        public string GetDisplayName()
        {
            string modeTag = Mode == GameMode.FirstTo5 ? " [до 5]" : " [∞]";
            if (IsFull)
                return $"🔴 {HostName} vs {GuestName} (Играют){modeTag}";
            else
                return $"🟢 Игра {HostName} (Ожидает){modeTag}";
        }
    }
}
