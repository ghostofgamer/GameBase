using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Base.Platform.VKGames
{
    /// <summary>
    /// Облачное сохранение через хранилище VK (VKWebAppStorageSet / VKWebAppStorageGet).
    /// Данные привязаны к пользователю VK, а не к устройству.
    ///
    /// Одно значение хранилища вмещает около 2000 символов, поэтому мост режет JSON на куски
    /// и пишет их в один из двух слотов по очереди, а номер слота в конце. Лимит VK: 1000 вызовов
    /// в час на пользователя, одно сохранение стоит (число кусков по 1000 символов + 1) вызовов.
    ///
    /// Бюджет вызовов считается здесь, по скользящему часу: пока запас есть, запись идёт сразу; когда запаса нет,
    /// последнее сохранение откладывается и уходит, как только вызовы освободятся (промежуточные не нужны — каждое
    /// сохранение целиком заменяет прошлое). Так игра не упирается в лимит VK, сколько бы раз её ни просили сохранить.
    /// </summary>
    public sealed class VKGamesCloudSaveService : ICloudSaveService
    {
        /// <summary>Вызовов в час на сохранения: из лимита VK 1000 — запас на чтение при запуске и повторы.</summary>
        private const int HourlyBudget = 800;

        private const int ChunkSize = 1000;
        private const float Hour = 3600f;

        // Время (Time.realtimeSinceStartup) каждого вызова хранилища за последний час.
        private readonly Queue<float> _calls = new Queue<float>();
        private string _pending;
        private bool _waiting;

        public bool IsAvailable => VKGamesBridge.IsSupported && VKGamesSession.IsInitialized;

        public async UniTask SaveAsync(string json, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable || string.IsNullOrEmpty(json))
                return;

            var cost = Cost(json);
            Forget();
            if (_calls.Count + cost > HourlyBudget)
            {
                // Лимит близко: держим последнее сохранение и пишем, когда час освободит вызовы.
                _pending = json;
                if (!_waiting)
                    WriteLaterAsync().Forget();
                Debug.LogWarning($"[VKGames] Save postponed: {_calls.Count} storage calls in the last hour");
                return;
            }

            _pending = null;
            await WriteAsync(json, cost, cancellationToken);
        }

        private async UniTask WriteAsync(string json, int cost, CancellationToken cancellationToken)
        {
            var now = Time.realtimeSinceStartup;
            for (var i = 0; i < cost; i++)
                _calls.Enqueue(now);

            try
            {
                await VKGamesBridge.SaveAsync(json, cancellationToken);
            }
            catch (VKGamesBridgeException e)
            {
                Debug.LogWarning($"[VKGames] Save failed: {e.Message}");
            }
        }

        /// <summary>Ждать, пока старые вызовы выйдут из часа, и записать последнее отложенное сохранение.</summary>
        private async UniTaskVoid WriteLaterAsync()
        {
            _waiting = true;
            try
            {
                while (_pending != null)
                {
                    var cost = Cost(_pending);
                    Forget();
                    if (_calls.Count + cost <= HourlyBudget)
                    {
                        var json = _pending;
                        _pending = null;
                        await WriteAsync(json, cost, CancellationToken.None);
                        break;
                    }

                    // Сколько ждать, пока освободится нужное число вызовов: по самому старому в очереди.
                    var wait = _calls.Count > 0 ? _calls.Peek() + Hour - Time.realtimeSinceStartup : 1f;
                    await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Clamp(wait, 1f, Hour)), ignoreTimeScale: true);
                }
            }
            finally
            {
                _waiting = false;
            }
        }

        /// <summary>Выбросить вызовы старше часа.</summary>
        private void Forget()
        {
            var edge = Time.realtimeSinceStartup - Hour;
            while (_calls.Count > 0 && _calls.Peek() < edge)
                _calls.Dequeue();
        }

        /// <summary>Сколько вызовов хранилища стоит сохранение: куски по 1000 символов и номер слота.</summary>
        private static int Cost(string json)
        {
            return (json.Length + ChunkSize - 1) / ChunkSize + 1;
        }

        public async UniTask<string> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return null;

            try
            {
                return await VKGamesBridge.LoadAsync(cancellationToken);
            }
            catch (VKGamesBridgeException e)
            {
                // Пробрасываем: "не прочиталось" нельзя путать с "сохранения нет",
                // иначе игра начнёт с нуля и затрёт прогресс игрока.
                Debug.LogWarning($"[VKGames] Load failed: {e.Message}");
                throw;
            }
        }
    }
}
