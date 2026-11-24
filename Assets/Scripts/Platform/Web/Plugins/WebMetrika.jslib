// Яндекс Метрика для web-площадок (Яндекс Игры, VK): события игры уходят «параметрами визитов».
// Счётчик подключает страница (WebGLTemplates/*/index.html, номер в window.baseMetrikaId).
// Нет счётчика или Метрика заблокирована — вызовы молча ничего не делают.

mergeInto(LibraryManager.library, {

  // Событие name с параметрами (JSON-дерево папок) или без них: в отчёте «Параметры визитов» — name → дерево.
  WebMetrikaEvent: function (namePtr, jsonPtr) {
    try {
      if (!window.baseMetrikaId || typeof window.ym !== 'function') return;
      var name = UTF8ToString(namePtr);
      var json = jsonPtr ? UTF8ToString(jsonPtr) : '';
      var params = {};
      params[name] = json ? JSON.parse(json) : 1;
      window.ym(window.baseMetrikaId, 'params', params);
    } catch (e) {
      console.warn('[Metrika] event failed', e);
    }
  },

  // Свойство игрока: отчёт «Параметры посетителей».
  WebMetrikaUserParam: function (keyPtr, valuePtr, number, isNumber) {
    try {
      if (!window.baseMetrikaId || typeof window.ym !== 'function') return;
      var params = {};
      params[UTF8ToString(keyPtr)] = isNumber ? number : UTF8ToString(valuePtr);
      window.ym(window.baseMetrikaId, 'userParams', params);
    } catch (e) {
      console.warn('[Metrika] userParams failed', e);
    }
  },

  // Подключён ли счётчик на странице.
  WebMetrikaIsAvailable: function () {
    return (window.baseMetrikaId && typeof window.ym === 'function') ? 1 : 0;
  }
});
