using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Base.Platform
{
    public sealed class LeaderboardEntry
    {
        public int Rank;
        public long Score;
        public string PlayerName;
        public bool IsCurrentPlayer;
    }

    /// <summary>Таблицы лидеров.</summary>
    public interface ILeaderboardService
    {
        bool IsAvailable { get; }

        /// <summary>
        /// Отправить результат. true — площадка записала; false — не записано (игрок не вошёл, нет таблиц, ошибка сети):
        /// игра оставит результат в очереди и отправит позже. Отличать «записано» от «пропущено» обязательно, иначе
        /// результат вошедшего позже игрока потеряется.
        /// </summary>
        UniTask<bool> SubmitScoreAsync(string boardId, long score, CancellationToken cancellationToken = default);

        UniTask<IReadOnlyList<LeaderboardEntry>> GetTopAsync(string boardId, int count, CancellationToken cancellationToken = default);

        /// <summary>Запись текущего игрока или null, если игрок не авторизован или не в таблице.</summary>
        UniTask<LeaderboardEntry> GetPlayerEntryAsync(string boardId, CancellationToken cancellationToken = default);
    }
}
