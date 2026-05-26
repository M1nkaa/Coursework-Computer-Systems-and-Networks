namespace RPS.Shared
{
    // Возможные фигуры: камень, бумага, ножницы
    public enum Choice
    {
        None,
        Rock,
        Paper,
        Scissors
    }

    // Результат раунда с точки зрения одного игрока
    public enum GameResult
    {
        None,
        Win,
        Lose,
        Draw
    }

    // Режим игры: бесконечные раунды или серия до 5 побед
    public enum GameMode
    {
        Infinite,       // Бесконечный режим
        FirstTo5        // Первый до 5 побед
    }

    // Типы сетевых сообщений между клиентом и сервером
    public enum MessageType
    {
        Connect,
        Disconnect,
        CreateGame,
        GetGamesList,
        GamesList,
        JoinGame,
        GameJoined,
        GameFull,
        GameStarted,
        MakeChoice,
        OpponentMadeChoice,
        GameResult,
        OpponentLeft,
        LeaveGame,
        ChatMessage,
        Error,
        PlayerAfk,          // Игрок кикнут за AFK
        OpponentAfk,        // Оппонент кикнут за AFK — этому игроку засчитывается победа
        TimerUpdate,        // Обновление таймера (секунды)
        GameOver,           // Конец серии (режим FirstTo5)
        RematchRequest,     // Игрок хочет сыграть ещё
        RematchAccepted,    // Оба согласны — новая игра в том же лобби
        RematchDeclined,    // Один из игроков отказался / вышел в лобби
        AnimationDone       // Клиент завершил анимацию, таймер можно запускать
    }
}
