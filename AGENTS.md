# Base: шаблон игр, структура и правила

Шаблон на Unity 6000.3 (URP) для мобильных и казуальных игр под площадки: Яндекс Игры (WebGL),
Игры ВКонтакте (WebGL, VK Bridge), RuStore (Android). Заготовка VK Play (vkplay.ru, свой API) есть, но площадка не планируется.
Площадка выбирается на этапе сборки. Каждая новая игра создаётся копией шаблона (GitHub: «Use this template»),
поэтому всё в `Assets/Scripts` не зависит от конкретной игры и называется нейтрально: `Base.*`.

- **Код шаблона** (`Assets/Scripts`, сборки `Base.*`) в игре не переименовывается. Исправления, сделанные в игре,
  которые касаются шаблона, переносятся в шаблон, и наоборот.
- **Код игры** живёт в `Assets/<Название>Content` (например, `Assets/ZonkContent` у Зонка) со своей сборкой (asmdef) и своим пространством имён.
  Он ссылается на `Base.Core`, `Base.Services`, `Base.Platform.Abstractions`, UniTask, Zenject. Сборки шаблона
  на код игры не ссылаются. Сборку игры нужно добавить в `Assets/link.xml` (`preserve="all"`), иначе Zenject
  в IL2CPP-сборке не создаст её классы.
- **Что своё у каждой игры**: список в README, раздел «Новая игра из шаблона».

## Стек и соглашения

- **Анимации: DOTween** (бесплатная версия из Asset Store, лежит в `Assets/Plugins/Demigiant`, define `DOTWEEN`), ожидание твинов через UniTask (`TweenAwait`).
- **Async: только UniTask.** Корутины не используются. Все асинхронные методы возвращают `UniTask` / `UniTask<T>`
  и принимают `CancellationToken`. Fire-and-forget только через `.Forget()`.
- **DI: Zenject (Extenject из Asset Store, лежит в `Assets`, сборка `Zenject`).** Не ставить через OpenUPM или git, чтобы не было двух копий. Зависимости через конструктор. Никаких `FindObjectOfType`, синглтонов и статических сервисов.
  Единственный `ProjectContext` живёт в `Assets/Resources/ProjectContext.prefab` (создать: меню `Base/Setup/Create ProjectContext`).
- **Площадка выбирается define:** `BASE_YANDEX`, `BASE_VKGAMES`, `BASE_VKPLAY`, `BASE_RUSTORE`. Ровно один в билде. Без define работает Stub.
- **В редакторе всегда Stub**, независимо от define. SDK площадок работают только в реальном билде.
- Код игры и `Base.Services` работают с площадкой **только через интерфейсы** из `Base.Platform.Abstractions`.
  Прямые ссылки на Yandex/VKGames/VKPlay/RuStore из кода игры запрещены.
- **Качество графики** игра получает только через `IQualityService` (уровни Low/Medium/High).
  Уровень выбирается по `IPlatformService.Device` и включает Quality Level Unity с тем же смыслом.
  Что меняется на уровне (URP-ассет, render scale, ужатие текстур), настраивается в Project Settings > Quality, не в коде.
- **Аналитика только через `IAnalytics`** (`Base.Services.Analytics`): имя события — «папка» (категория), `Path` — вложенные
  папки, `Param` — значения. Имена латиницей в snake_case, в значениях ID контента. Прямые вызовы AppMetrica из игры запрещены.
  События редкие и значимые (не каждый кадр и не каждый бросок).
- **Сохранение только через `ISaveStore`**, не через `ICloudSaveService` напрямую. Каждая система игры берёт свой раздел
  (`Get<T>(key)`, `[Serializable]`-класс для JsonUtility). Переименовать или перенести поле раздела можно только
  вместе с новой `ISaveMigration` (FromVersion = текущая версия раздела). Старые миграции не удалять.
  Добавлять новые поля можно без миграции.
  **Сторож обновлений** — EditMode-тест игры: сохранение каждого выпуска (`Tests/Saves/save_<версия>.json`) читается без потерь,
  ID контента выпуска на месте: наследник `SaveCompatibilityTestBase`, см. «Общие части игры».
- **Защита покупок:** права за деньги выдаёт только `IPurchaseFlow`; при запуске `PurchaseFlow` сверяет постоянные права со списком
  площадки (`IPurchaseService.GetPurchasesAsync`, `ListsPermanentPurchases`) и снимает неподтверждённые. Проверка покупки
  перед выдачей — `IPurchaseVerifier` (с сервером — проверка подписи). Новая площадка обязана отличать «не ответила» от «пусто».
- **Реклама и покупки в игре только через общий слой:** `IInterstitialService.TryShowAsync(trigger)` на естественных паузах
  (конец партии, перезапуск), `IRewardService.RequestAsync(placement)` для наград, `IPurchaseFlow` для покупок,
  `IEntitlements` для проверки прав. Прямые вызовы `IAdsService` / `IPurchaseService` из игры запрещены:
  иначе обходятся частота рекламы, `no_ads` и защита покупок. Своё для каждой игры задаётся в `MonetizationConfig`.
- **Stripping Level High + `Assets/link.xml`.** Любая новая сборка, чьи классы биндятся в Zenject или вызываются из JS/Android через reflection, добавляется в `link.xml` с `preserve="all"`.
- Каждое изменение проекта записывается в `README.md` (раздел «Журнал изменений»). Этот файл держим актуальным при изменении структуры.
- Комментарии и документация на русском, идентификаторы на английском.

## Общие части игры (Base.Kit)

Сборка `Base.Kit` (`Assets/Scripts/Kit`) — то, что нужно в каждой игре поверх площадки: окна, звук, музыка, анимации,
настройки игрока. Без спрайтов, моделей и стиля: вид — у игры (детали-префабы, шрифты, цвета в её конфигах).

- **Подключение.** В MonoInstaller игры (ProjectContext): `KitBindings.InstallProject(Container, soundConfig, uiConfig)` —
  `IGameSettings`, `SoundConfig`, `UiConfig`. В SceneContext сцены с интерфейсом — `KitSceneInstaller` (корень UI с `UiSafeArea`,
  `SoundPlayer`, `MusicPlayer`): он биндит `IUiService`, `UiBackButton`, звуки и петли (`MusicPlayer` по `LoopChannel`).
  Ссылки в сцене проставляет генератор игры (`EditorSetup`), поиска по сцене нет.
- **Окна.** Каждое окно — префаб с наследником `UiWindow` в `UiConfig.WindowsFolder`; список `UiConfig.Windows` собирается сам
  (`Base/UI/Rebuild Windows`). Открывается `IUiService.OpenAsync<T>(ct, setup)` (создаётся из префаба), закрывается
  `CloseAsync` (удаляется): в памяти только видимое. Появление и скрытие — ассеты `UiTransitionConfig` (эффекты
  `[SerializeReference, SubclassSelector]`, новый эффект — наследник `UiEffect`), не код окна. Кнопка «Назад» Android и Escape
  нажимает `BackButton` верхнего окна; корневой экран (`IsRootScreen`) на Android спрашивает о выходе (`IQuitPrompt` игры).
  Мимолётное (Toast, всплывающие очки) — из `UiPool<T>` по префабу-детали, не `Instantiate` на каждый показ.
- **Детали окон** (`UI/Views`): `UiButtonView` (шаблон кнопки, `Spawn` для списков), `UiPress` (вдавливание и щелчок),
  `UiSway` (качание, подпрыгивание, пульсация), `SpriteFlipbook` (смена кадров), `VolumeSliderView` (ползунок громкости,
  окно связывает его `UiWindow.BindVolume`), `UiFitHeight` (колонка ужимается, если не влезает), `UiLabelPlate` (надпись на подложке).
  Префабы деталей — у игры (`Prefabs/UI/Parts`), шаблон даёт только компоненты.
- **Звук.** `SoundPlayer.Play(id)`: ID строкой — `UiSfx.*` для интерфейса, константы игры для остального; клипы и громкость —
  строки `SoundConfig`. Без клипов — заглушка: встроенная у звуков интерфейса, у своих — `SoundPlayer.SetFallback` с синтезом
  `ProceduralSounds`. Щелчок кнопки даёт `UiPress`, открытие окна — `UiService` (молчащее окно — `PlaysOpenSound`): в коде окна
  щелчок на кнопку не вызывать, будет двойной. Громкость — ползунки «Звуки» и «Музыка» (`IGameSettings`), выключателей нет
  (0 — выключено), группы громкости в коде, не AudioMixer: в WebGL его нет. Новый звук мимо `SoundPlayer` не заводить.
- **Музыка.** `MusicPlayer.Play(AssetReferenceT<AudioClip>)` / `Stop()`: переход между треками, в памяти один трек
  (Addressables), канал `Ambience` — фон окружения, `PlayOverlay` — дорожка поверх фона. Приглушение под важный звук —
  `SoundConfig.Entry.MusicDuck`. В путях ассетов Addressables нет квадратных скобок: в редакторе загрузка падает.
- **Настройки игрока** — `IGameSettings` (громкость, вибрация, язык; раздел сохранения `settings`). Свои настройки игры —
  отдельным сервисом в своём разделе. В `Construct`/`Awake` не читать: сохранение может быть ещё не загружено (`ISaveStore.IsLoaded`).
- **Анимации.** Твин ждётся `await tween.AwaitAsync(ct)` (`Base.Tweens.TweenAwait`): отмена токена убивает твин.
  Модули DOTween (`DOTweenModuleUI` и др.) из asmdef-сборок не видны, UI-твины — через ядро: `DOTween.To(getter, setter, …)`.
  Движение объектов без DOTween — `Animate.*` (без замыканий), сглаживание — поля `AnimateEase`.
- **Тексты.** `UiFormat.Format` — редкое (открытие окна); часто меняющееся число — `NumberTemplate` / `NumberText`
  (без выделения памяти, текст меняется только при новом значении). Ключи шаблона — `KitTexts`, ru и en в `LocalizationTable`.
- **Производительность (телефоны и браузер).** В WebGL сборщик мусора останавливает всю игру, поэтому код, который
  работает каждый кадр или на каждое событие игры, не создаёт объектов: рабочие списки — поля, очищаемые перед использованием;
  без LINQ, замыканий, `GetComponent` и сборки строк в таких местах; списки по `IReadOnlyList` — `for`, не `foreach`.
  В цикле на каждый кадр — `UniTask.Yield()` и проверка `ct.IsCancellationRequested`. Редкое (открытие окна) может выделять память.
- **Сторож сохранений.** Тест игры — наследник `SaveCompatibilityTestBase` (`Base.Testing`): папка снимков, все разделы, класс
  ключей, текущие ID контента. Снимок — `Base/Saves/Export Release Fixture` в день выпуска (папку и ID даёт `IReleaseFixtureSource`
  в Editor-сборке игры), старые снимки не удалять. Новый раздел сохранения — строка в `Sections`.
- **Вёрстка UI под любой экран.** `CanvasScaler`: Scale With Screen Size, 1920×1080, режим Expand. Окна и панели не больше
  1760×1000. Элемент у края — якорями к своему углу или краю, растягиваемое — stretch. Корень UI сцены — `UiSafeArea`.
  Списки — `ScrollRect` + `VerticalLayoutGroup` + `ContentSizeFitter`, строки на `LayoutElement`. Текст — TMP с автоподбором
  размера и переносом, проверять на самом длинном языке (de, fr, pt) и на 16:9, 21:9, 4:3, 1:1, 9:16.

## Структура

```
Assets/
  Scripts/
    Core/                 Base.Core.asmdef       чистый C# без UnityEngine: локализация
      Localization/         ILocalization, Localization, LocalizationTable: тексты интерфейса, языки ru и en
                            ILocalizationSource, CsvLocalizationParser: тексты игры из CSV (Google Таблица)
    Services/             Base.Services.asmdef   общий слой игры поверх площадки; ссылается на Core и Abstractions
      Debugging/PlatformTestPanel.cs  отладочная панель: кнопка на каждый метод платформенных интерфейсов, UI строится в коде
      Quality/              IQualityService, QualityService, QualityTier: уровень качества по типу устройства
      Haptics/              IHapticsService, HapticsService: короткая вибрация (Android); в браузере и редакторе недоступна
      Analytics/            IAnalytics, Analytics, AnalyticsEvent: события по «папкам» (имя события → Path → Param) поверх IAnalyticsService
      Texts/                TextAssetLocalizationSource: CSV-файл игры как источник ILocalization
      Saves/                ISaveStore, SaveStore, ISaveMigration: сохранение по разделам с версиями поверх ICloudSaveService
      Monetization/         MonetizationConfig    товары, частота рекламы, правила наград: правится в каждой игре
                            IEntitlements         права игрока (no_ads и др.) в разделе сохранения
                            IPurchaseFlow         покупка, выдача, восстановление при запуске
                            IRewardService        награда за рекламу или бесплатно по правилам
                            IInterstitialService  межстраничная реклама по поводу с ограничением частоты
                            AdPauseController     пауза и звук на время рекламы и при потере фокуса
      GameServicesInstaller.cs  биндинги общего слоя, вызывается из PlatformInstaller
    Platform/
      Abstractions/       Base.Platform.Abstractions.asmdef
                            IPlatformService     жизненный цикл SDK, язык, тип устройства, авторизация, GameReady/GameplayStart/Stop
                            IAdsService          interstitial, rewarded, события AdOpened/AdClosed для паузы и звука
                            IPurchaseService     каталог, покупка, pending-покупки, consume
                            ICloudSaveService    один JSON-блоб на игрока
                            ILeaderboardService  submit, top, запись игрока
                            IAnalyticsService    события и свойства игрока (RuStore — AppMetrica, Яндекс и VK — Яндекс Метрика)
                            DeviceKind, DeviceKinds  тип устройства и запасное определение через Unity
                            PlatformId, PlatformDefines
      Stub/               Base.Platform.Stub.asmdef     заглушки: редактор и билды без SDK (PlayerPrefs, мгновенный успех)
      Yandex/             Base.Platform.Yandex.asmdef   define BASE_YANDEX, платформы WebGL+Editor
                            YandexBridge          обмен с JS: номер запроса -> UniTaskCompletionSource
                            YandexBridgeReceiver  GameObject "YandexSdkBridge", принимает SendMessage из JS
                            YandexBridgeDto       классы ответов для JsonUtility
                            YandexSession         общее состояние: язык, авторизация, игрок
        Plugins/            YandexBridge.jslib    вызовы Yandex Games SDK
      VKGames/            Base.Platform.VKGames.asmdef  define BASE_VKGAMES, платформы WebGL+Editor
                            VKGamesBridge         обмен с JS по той же схеме, что у Яндекса
                            VKGamesBridgeReceiver GameObject "VKGamesSdkBridge"
                            VKGamesBridgeDto, VKGamesSession
        Plugins/            VKGamesBridge.jslib   вызовы VK Bridge: реклама, хранилище
      VKPlay/             Base.Platform.VKPlay.asmdef   define BASE_VKPLAY, заглушки с TODO; площадка не планируется (24.09)
        Plugins/            .jslib API VK Play (пока пусто)
      RuStore/            Base.Platform.RuStore.asmdef  define BASE_RUSTORE, платформы Android+Editor
                            RuStoreAdsService    Яндекс Реклама (пакет com.yandex.mobileads), предзагрузка обоих форматов
                            RuStoreAdUnits       идентификаторы рекламных блоков (сейчас демо)
                            RuStoreAppMetrica    запуск AppMetrica до первой сцены, API key приложения
                            RuStoreAnalyticsService  события и свойства игрока в AppMetrica
                            RuStoreLinker.xml    защита сборок Яндекса от стриппинга, только в Android-сборке
        Plugins/Android/    .aar SDK RuStore, когда понадобятся (пока пусто)
      Web/                Base.Platform.Web.asmdef      платформы WebGL+Editor, без define: общее для web-площадок
                            WebMetrikaAnalyticsService  аналитика Яндекс Игр и VK через Яндекс Метрику
        Plugins/            WebMetrika.jslib      вызовы ym(): события «параметрами визитов», свойства игрока
      Installers/         Base.Platform.Installers.asmdef
                            PlatformInstaller    единственное место выбора реализации по define
                            PlatformInitializer  IInitializable, запускает InitializeAsync площадки,
                                                 затем выставляет язык и качество, загружает сохранение,
                                                 затем NotifyGameReady, затем восстанавливает покупки
    Kit/                  Base.Kit.asmdef        общие части игры поверх площадки (раздел «Общие части игры»), без спрайтов и стиля
      UI/                   UiWindow, UiService (IUiService), UiConfig, UiBackButton (IQuitPrompt), UiSafeArea, UiPool<T>
        Transitions/        UiTransitionConfig, UiEffect и наследники (Fade, Slide, Move, Scale, Rotate, Punch, Shake, Stagger)
        Views/              UiButtonView, UiPress, UiSway, SpriteFlipbook, VolumeSliderView, UiFitHeight, UiLabelPlate
      Audio/                SoundPlayer, SoundConfig, ProceduralSounds, MusicPlayer (LoopChannel), UiSfx
      Settings/             IGameSettings, GameSettings: громкость, вибрация, язык (раздел сохранения settings)
      Tweens/               TweenAwait (DOTween через UniTask), Animate и AnimateEase (движение без замыканий)
      Text/                 UiFormat, NumberTemplate, NumberText, KitTexts
      Inspector/            SubclassSelectorAttribute для [SerializeReference]
      KitBindings, KitSceneInstaller  биндинги: проект (из инсталлера игры) и сцена с UI (SceneContext)
      Editor/               Base.Kit.Editor.asmdef  SubclassSelectorDrawer, UiWindowRegistry (Base/UI/Rebuild Windows),
                            SaveFixtureExporter (Base/Saves/Export Release Fixture, IReleaseFixtureSource игры)
      Testing/              Base.Testing.asmdef     SaveCompatibilityTestBase, JsonFlattener: основа теста сохранений игры
      Tests/                Base.Kit.Tests.asmdef   EditMode-тесты шаблона
    Editor/               Base.Editor.asmdef
                            PlatformSwitcher         меню Base/Platform: target, defines, шаблон, плагины
                            PlatformPluginToggler    включает .jslib/.aar только активной площадки
                            PlatformBuildPreprocessor проверка перед любой сборкой, падает при несоответствии
                            BuildScript              меню Base/Build и CLI-точки входа, результат в Builds/<площадка>
                            VKGamesDeployer          меню Base/Deploy: выкладка Builds/VKGames на хостинг VK (dev)
                            ProjectIdentity          имя компании, название, Android-пакет, номер счётчика Метрики; выставляются при переключении и сборке
                            PlatformLinkXml          дополнительный link.xml только для сборки нужной платформы (Android: RuStoreLinker.xml)
                            ProjectContextCreator    меню Base/Setup/Create ProjectContext
      Texts/                LocalizationSheet, LocalizationSheetDownloader: меню Base/Localization/Download Sheets
                            PlatformTestSceneCreator меню Base/Setup/Create Platform Test Scene, создаёт Scenes/PlatformTest.unity
                            PlatformTargets          соответствие площадка -> BuildTarget, шаблон, папка плагинов
  Plugins/Demigiant/      DOTween (бесплатная версия, Asset Store); настройки — Resources/DOTweenSettings.asset
  Plugins/Android/        mainTemplate.gradle, settingsTemplate.gradle, gradleTemplate.properties: пользовательские
                          gradle-шаблоны Unity. Единственное исключение из запрета Assets/Plugins: Unity ищет их только здесь.
                          Разделы "Android Resolver" в них пишет EDM4U, руками не править
  WebGLTemplates/
    Yandex/index.html     шаблон для Яндекс Игр: /sdk.js, YaGames.init() и чтение языка на странице
    VKGames/index.html    шаблон для Игр ВКонтакте: VKWebAppInit и параметры запуска на странице
    VKGames/vk-bridge.min.js  VK Bridge 3.0.2 (MIT), своя копия вместо CDN
    VKPlay/index.html     шаблон для VK Play, API ещё не подключён
  Resources/
    ProjectContext.prefab Zenject ProjectContext с PlatformInstaller (инсталлер игры добавляется после него)
    Fonts/Roboto-Regular.ttf  шрифт с кириллицей, Apache 2.0. Встроенный шрифт Unity в WebGL кириллицу не рисует
  Scenes/
    PlatformTest.unity    тестовая сцена с кнопкой на каждый метод площадки; в Build Settings выключена, первой идёт сцена игры
  link.xml                защита сборок Base.* и UniTask от стриппинга
  <Название>Content/     всё своё у игры: Scripts, Configs, Localization, Art, Prefabs, Scenes. В шаблоне папки нет
Packages/manifest.json    все сторонние пакеты — через git с тегом версии (без реестров): UniTask 2.5.11, com.yandex.mobileads 8.4.0,
                          адаптер AdRevenue 1.0.0, io.appmetrica.analytics 6.10.0, EDM4U 1.2.189 (пакеты Unity — URP, Addressables 2.8.1 — из реестра Unity). Git-пакеты Unity 6.3 не проверяет
                          на подпись, поэтому нет предупреждений «Signature: Missing». Обновление — сменой тега. Zenject не здесь, а в Assets
                          из Asset Store
vk-hosting-config.json    выкладка Builds/VKGames на хостинг VK, ID игры
```

## Как переключить площадку

Меню `Base/Platform/<площадка>`. Скрипт:
1. переключает активный Build Target (WebGL или Android),
2. ставит define площадки на нужный target и снимает BASE_* с остальных,
3. выставляет WebGL-шаблон (`PROJECT:Yandex` / `PROJECT:VKGames` / `PROJECT:VKPlay`),
4. выставляет сжатие WebGL под хостинг площадки (Яндекс: Brotli, остальные: gzip + Decompression Fallback),
5. включает нативные плагины только этой площадки.

`Base/Build` перед WebGL-сборкой удаляет папку `Builds/<площадка>`, чтобы на хостинг не уехали старые файлы.

`Base/Platform/Show Current` печатает текущее состояние в консоль.

## Как собрать

Меню `Base/Build/<площадка>` или CLI:

```
Unity -batchmode -quit -projectPath . -buildTarget WebGL   -executeMethod Base.Editor.BuildScript.BuildYandex
Unity -batchmode -quit -projectPath . -buildTarget WebGL   -executeMethod Base.Editor.BuildScript.BuildVKGames
Unity -batchmode -quit -projectPath . -buildTarget WebGL   -executeMethod Base.Editor.BuildScript.BuildVKPlay
Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod Base.Editor.BuildScript.BuildRuStore
```

Сборка через обычное окно Build тоже работает: `PlatformBuildPreprocessor` проверит, что defines, target,
шаблон и плагины согласованы, и остановит сборку с подсказкой, если нет.

## Как добавить SDK площадки

1. Нативные файлы (.jslib, .aar, .jar) класть **только** в `Platform/<площадка>/Plugins`. Никогда в `Assets/Plugins`.
   Исключения: gradle-шаблоны в `Assets/Plugins/Android` (Unity ищет их только там) и SDK, поставляемые UPM-пакетом
   с собственной `.aar` внутри пакета (Яндекс Реклама). Если SDK площадки нужно защитить от стриппинга,
   правила кладутся в `Platform/<площадка>/<Имя>Linker.xml` и подключаются через `PlatformLinkXml`, а не в общий `link.xml`.
2. C#-обёртки класть в `Platform/<площадка>/`, они компилируются только под своим define.
3. Заменить TODO-реализации сервисов в `Platform/<площадка>/` на вызовы SDK, сигнатуры интерфейсов не менять.
4. Для web-площадок раскомментировать подключение SDK в `WebGLTemplates/<площадка>/index.html`.
5. Android-зависимости версионировать явно (EDM4U или `mainTemplate.gradle`), чтобы конфликты ловились на gradle-резолве.
6. После добавления запустить `Base/Platform/<площадка>`, чтобы плагины получили правильные настройки импорта.

## Требования модерации Яндекс Игр

- `sdk.js` подключается **относительным** путём `/sdk.js` для игр на серверах Яндекса.
- **В файлах игры не должно быть абсолютного адреса хранилища SDK Яндекса**, даже в комментарии.
  Консоль разработчика сканирует архив и отклоняет загрузку с ошибкой
  «Файл содержит URL-адрес внутреннего хранилища сервиса». Абсолютный адрес нужен только
  при размещении на своём домене, его вид смотреть в документации, а не хранить в репозитории.
- `YaGames.init()` вызывается **самой страницей** сразу по загрузке скрипта, а не из Unity.
  Промис лежит в `window.ysdkReady`, мост его дожидается. Так платформа видит инициализацию,
  даже если Unity ещё грузится.
- `LoadingAPI.ready()` обязателен. Без него лоадер платформы висит в ожидании,
  и модерация отклоняет игру с формулировкой «SDK не встроено». Сейчас вызывается автоматически
  из `PlatformInitializer` после инициализации.
- **Язык интерфейса определяется автоматически** по `ysdk.environment.i18n.lang` (п. 2.14).
  Обращение к `environment.i18n.lang` происходит **на странице, в `initSDK()`**, сразу после `YaGames.init()`,
  и не зависит от того, успел ли запуститься Unity. Платформа отслеживает именно это обращение:
  на debug-панели индикатор 文 должен стать зелёным **на старте**, красный означает «I18N is not used».
  Мост берёт готовое значение из `window.ysdkLang`.
  `PlatformInitializer` передаёт `IPlatformService.Language` в `ILocalization.SetLanguage`.
  Любой новый текст в UI берётся через `ILocalization.Get(key)`, хардкод строк на русском запрещён.
  Неподдерживаемые языки откатываются на английский.
- Проверять подключение нужно в Консоли разработчика кнопкой «Открыть с debug-панелью»:
  индикатор лоадера слева внизу должен показывать `IT`. `W` значит, что инициализации не было,
  `IF` значит устаревший способ подключения. Индикатор 文 рядом должен быть зелёным.
- **Весь текст в UI только шрифтом с кириллицей** (`Resources/Fonts/Roboto-Regular.ttf`).
  Встроенный шрифт Unity в WebGL рисует латиницу и цифры, а кириллицу нет: русские надписи просто исчезают.
  В редакторе проблема не воспроизводится, поэтому проверять только в браузере.

## Особенности Игр ВКонтакте (VK Bridge)

Документация: раздел games на dev.vk.com. Факты ниже проверены по ней 11 сентября 2026 года.

- `VKWebAppInit` отправляет **сама страница** сразу при загрузке: VK требует его первым событием
  и не позже 30 секунд после запуска. Промис лежит в `window.vkInitPromise`, мост его дожидается.
- Параметры запуска (`vk_language`, `vk_platform`, `vk_user_id`) страница кладёт в `window.vkLaunchParams`.
  Отдельной авторизации нет, игрок VK известен по `vk_user_id`.
- Аналогов `LoadingAPI.ready` и `GameplayAPI` у VK нет, эти вызовы на VK ничего не делают.
- Реклама: `VKWebAppShowNativeAds` с `interstitial` и `reward`, для rewarded водопад выключен.
  `VKWebAppCheckNativeAds` при отсутствии рекламы только запускает загрузку и отвечает false, поэтому мост
  предзагружает оба формата после инициализации и раз в 30 секунд, а показ вызывает всегда, без проверки.
  Кнопку rewarded в игре показывать по `IAdsService.IsRewardedAvailable` (на VK это «реклама предзагружена»).
  Событий открытия и закрытия у VK нет, мост отправляет `adOpened`/`adClosed` сам вокруг вызова показа.
  Нельзя показывать рекламу сразу после запуска игры.
- Хранилище: значение до ~2000 символов, 1000 ключей и **1000 вызовов в час** на пользователя.
  Мост режет сохранение на куски по 1000 символов и пишет в два чередующихся слота.
- **Лидерборды и покупки требуют своего сервера.** Очки сохраняются только серверным `secure.addAppEvent`,
  покупки подтверждаются обратными вызовами VK на сервер игры. Сейчас оба сервиса на VK недоступны (`IsAvailable = false`).
- Модерация VK: основной язык русский, есть выключение звука, обучение с подсказками, канал поддержки.
- **Выкладка на хостинг VK.** Загрузки архива через сайт, как у Яндекса, у VK нет: только npm-пакет
  `@vkontakte/vk-miniapps-deploy`. Лимит 24 выкладки в сутки, архив до 300 МБ.
  - Для тестов: меню `Base/Build/VK Games + Deploy (dev)` собирает и выкладывает одной кнопкой
    (`VKGamesDeployer`). Выкладывается только режим разработки: без вопросов и без подтверждения на телефоне.
    Игру видят администраторы, если в «Размещении» стоит галочка «Режим разработки». Готовая сборка
    выкладывается отдельно через `Base/Deploy/VK Games (dev)`.
  - Вход в VK нужен один раз: `npx @vkontakte/vk-miniapps-deploy` в PowerShell из корня проекта, открыть ссылку.
    Токен хранится у пользователя (configstore), в проект не попадает. Если Unity сообщит, что нужен вход, повторить.
  - Прод (перед модерацией) выкладывается вручную той же командой из PowerShell (интерактивно: из фона не подтвердить).
    Готовый конфиг прода — папка `Temp/VKGamesDeployProd` (`update_prod: 1`, `noprompt: 1`). После «Please, confirm deploy
    on your phone» VK присылает **код в сообщения VK от «Администрации»** (не пуш), утилита ждёт его ввода (19.11). Ошибка
    `invalid json … <!DOCTYPE` при загрузке — сбой сервера VK после частых загрузок: подождать и повторить.
  - Конфиг `vk-hosting-config.json` в корне хранит ID игры и путь `Builds/VKGames`.
- **Хостинг VK не отдаёт заголовок `Content-Encoding`** для `.br`/`.gz` (проверено 11.09.2026: `binary/octet-stream`),
  и сборка с обычным сжатием не загружается («Unable to parse … web server … misconfigured»). Поэтому для VK
  сжатие gzip с `Decompression Fallback`: загрузчик Unity распаковывает файлы сам. Сжатие каждой площадки задаёт
  `PlatformTargets.WebGLCompressionFor`, его выставляют `PlatformSwitcher` и `PlatformBuildPreprocessor`.
  У Яндекса Brotli без fallback, их хостинг заголовок отдаёт.
- **Адрес игры на хостинге VK меняется при каждой выкладке**, поэтому `PlayerPrefs`, cookies и localStorage
  после обновления теряются. Всё, что должно сохраниться, хранить через `ICloudSaveService`.
- Игра, пока выключена в настройках, открывается только администраторам и тестировщикам: `https://vk.com/app<ID>`.
  Реклама до модерации показывается в тестовом режиме.

## Особенности RuStore (Android)

Документация: rustore.ru/help. Факты ниже проверены 11 сентября 2026 года.

- **Своей рекламной сети у RuStore нет.** Реклама идёт через Яндекс Рекламу (пакет `com.yandex.mobileads` 8.4.0).
  API версии 8: `InterstitialAdLoader` / `RewardedAdLoader`, `new AdRequest(id)`, события `OnAdShown`,
  `OnAdDismissed`, `OnAdFailedToShow`, `OnRewarded`. Колбэки приходят в главный поток.
  Демо-блоки `demo-interstitial-yandex` / `demo-rewarded-yandex` работают без регистрации,
  свои блоки заводятся в Рекламной сети Яндекса (partner.yandex.ru) и прописываются в `RuStoreAdUnits`.
- Android-зависимости SDK разрешает EDM4U в gradle-шаблоны `Assets/Plugins/Android`.
- **Лидербордов, имени игрока и авторизации у RuStore нет.** Облачное сохранение есть только в GameCenter SDK (бета):
  нужен установленный RuStore с входом и подключённый Pay SDK. Пока сохранение локальное (PlayerPrefs).
- **Платежи (Pay SDK, `ru.rustore.pay`)** только для ИП и юрлиц, заявка подписывается УКЭП. Отложены.
  Pay SDK работает только с `UnityPlayerActivity`, а Unity 6 по умолчанию ставит GameActivity: при подключении переключить.
- Maven-репозиторий SDK RuStore сменил адрес: использовать `https://nexus-external.rustore.ru/repository/maven-rustore-exposed`,
  старый `artifactory-external.vkpartner.ru` не использовать.
- Публикация: APK или AAB, свой ключ подписи. **Идентификатор пакета после публикации не меняется** (`ProjectIdentity`).
  Нужны возрастной рейтинг, описание на русском, 3 скриншота, иконка 512×512. Модерация обычно до 3 рабочих дней.
- Папку `Builds/RuStore/*_BurstDebugInformation_DoNotShip` в магазин не загружать.

## Мост Unity и JavaScript (Яндекс и Игры ВКонтакте)

Все вызовы SDK асинхронные, поэтому мост построен на номерах запросов:

1. C# берёт следующий номер, кладёт `UniTaskCompletionSource` в словарь и зовёт функцию из `.jslib`.
2. JS выполняет вызов SDK и отвечает через `Module.SendMessage("YandexSdkBridge", "OnBridgeResponse", json)`.
3. `YandexBridgeReceiver` отдаёт ответ в `YandexBridge`, тот находит источник по номеру и завершает задачу.

Ответ всегда `{id, ok, result, error}`, где `result` это строка с JSON (JsonUtility не умеет вложенные `object`).
События рекламы идут отдельным каналом `OnBridgeEvent` со строками `adOpened` и `adClosed`.

Имена объектов `YandexSdkBridge` / `VKGamesSdkBridge` и имена методов связаны с `.jslib` строками, менять только вместе.
Механика запросов в `YandexBridge` и `VKGamesBridge` пока продублирована. Когда появится третий web-мост (VK Play),
вынести её в общую сборку.
`Module.SendMessage` используется вместо `window.unityInstance`, потому что доступен сразу и не зависит от загрузки страницы.

## Что не трогать

- `Assets/TutorialInfo`, `Assets/Readme.asset`, `SampleScene`: удалены (остатки шаблона Unity; в шаблоне — 24.11).
- `Far.sln`: дубликат, можно удалить. Файлы `.sln` и `.csproj` Unity создаёт сам по имени папки проекта.
