using System;

namespace RPS.Shared
{
    // Данные о результате игры
    [Serializable]
    public class GameData
    {
        // Что выбрал игрок
        public Choice PlayerChoice { get; set; }

        // Что выбрал соперник
        public Choice OpponentChoice { get; set; }

        // Результат (победа/поражение/ничья)
        public GameResult Result { get; set; }

        // Счёт игрока
        public int PlayerScore { get; set; }

        // Счёт соперника
        public int OpponentScore { get; set; }

        // Имя соперника
        public string OpponentName { get; set; }
    }

    // Статистика игрока
    [Serializable]
    public class PlayerStats
    {
        public string Name { get; set; }
        public int Wins { get; set; }      // Побед
        public int Losses { get; set; }    // Поражений
        public int Draws { get; set; }     // Ничьих

        // Всего игр
        public int TotalGames => Wins + Losses + Draws;
    }
}