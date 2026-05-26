using System;

namespace RPS.Shared
{
    // Результат одного раунда — сервер отправляет каждому игроку после того,
    // как оба сделали выбор
    [Serializable]
    public class GameData
    {
        public Choice PlayerChoice { get; set; }
        public Choice OpponentChoice { get; set; }
        public GameResult Result { get; set; }
        public int PlayerScore { get; set; }
        public int OpponentScore { get; set; }
        public string OpponentName { get; set; }
        public bool IsGameOver { get; set; }       // Серия завершена (FirstTo5)
        public string GameOverWinner { get; set; } // Имя победителя серии
    }

    // Накопленная статистика игрока (победы, поражения, ничьи)
    [Serializable]
    public class PlayerStats
    {
        public string Name { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int TotalGames => Wins + Losses + Draws;
    }
}
