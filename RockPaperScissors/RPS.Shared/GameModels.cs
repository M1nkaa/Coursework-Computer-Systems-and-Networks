namespace RPS.Shared
{
    // Перечисление выборов игрока
    public enum Choice
    {
        None,      // Ничего не выбрано
        Rock,      // Камень
        Paper,     // Бумага
        Scissors   // Ножницы
    }

    // Результат игры
    public enum GameResult
    {
        None,  // Результата ещё нет
        Win,   // Победа
        Lose,  // Поражение
        Draw   // Ничья
    }

    // Типы сообщений между клиентом и сервером
    public enum MessageType
    {
        Connect,           // Подключение к серверу
        Disconnect,        // Отключение
        CreateGame,        // Создать новую игру
        GetGamesList,      // Получить список игр
        GamesList,         // Ответ со списком игр
        JoinGame,          // Присоединиться к игре
        GameJoined,        // Успешно присоединился
        GameFull,          // Игра заполнена
        GameStarted,       // Игра началась
        MakeChoice,        // Игрок сделал выбор
        OpponentMadeChoice, // НОВОЕ: Соперник сделал выбор
        GameResult,        // Результат раунда
        OpponentLeft,      // Соперник вышел
        LeaveGame,         // Покинуть игру
        ChatMessage,       // Сообщение в чат
        Error              // Ошибка
    }
}