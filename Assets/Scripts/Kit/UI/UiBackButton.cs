using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Base.UI
{
    /// <summary>
    /// Вопрос «Выйти из игры?» для кнопки «Назад» на корневом экране Android. Окно вопроса — своё у игры.
    /// Не забинжено — выход без вопроса.
    /// </summary>
    public interface IQuitPrompt
    {
        UniTask<bool> AskQuitAsync(CancellationToken ct);
    }

    /// <summary>
    /// Кнопка «Назад» Android и Escape (Input System приводит их к одной клавише): действует верхнее окно —
    /// нажимает его BackButton (UiWindow.OnBackPressed). На корневом экране (UiWindow.IsRootScreen) на Android —
    /// вопрос о выходе; в браузере выйти некуда.
    /// </summary>
    public sealed class UiBackButton : ITickable, IDisposable
    {
        private readonly IUiService _ui;
        private readonly IQuitPrompt _quit;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _askingQuit;

        public UiBackButton(IUiService ui, [InjectOptional] IQuitPrompt quit = null)
        {
            _ui = ui;
            _quit = quit;
        }

        public void Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            var top = _ui.Top;
            if (top == null || !top.AcceptsBack)
                return;

            if (top.IsRootScreen)
            {
                if (Application.platform == RuntimePlatform.Android)
                    AskQuitAsync(_cts.Token).Forget();
                return;
            }

            top.OnBackPressed();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid AskQuitAsync(CancellationToken ct)
        {
            if (_askingQuit)
                return;

            _askingQuit = true;
            try
            {
                if (_quit == null || await _quit.AskQuitAsync(ct))
                    Application.Quit();
            }
            catch (OperationCanceledException)
            {
                // Закрылась сцена: спрашивать уже незачем.
            }
            finally
            {
                _askingQuit = false;
            }
        }
    }
}
