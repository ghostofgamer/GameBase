using System.Collections.Generic;
using UnityEngine;

namespace Base.UI
{
    /// <summary>
    /// Пул мимолётных объектов интерфейса из префаба-детали (надпись Toast, всплывающие очки, летящая монетка):
    /// объект создаётся один раз и дальше только переносится. Частое всплывающее — только отсюда, не Instantiate.
    /// Свободные ждут под выключенным объектом сцены и уничтожаются вместе с ней; уничтоженные пропускаются.
    /// </summary>
    public sealed class UiPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Stack<T> _free = new Stack<T>();
        private Transform _root;

        public UiPool(T prefab)
        {
            _prefab = prefab;
        }

        public bool HasPrefab => _prefab != null;

        /// <summary>Объект из пула в parent, включённый и поверх соседей. Нет префаба — null.</summary>
        public T Rent(Transform parent)
        {
            T item = null;
            while (_free.Count > 0 && item == null)
                item = _free.Pop();

            if (item == null)
            {
                if (_prefab == null)
                    return null;
                item = Object.Instantiate(_prefab, parent);
            }

            item.transform.SetParent(parent, false);
            item.transform.SetAsLastSibling();
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>Вернуть в пул. Объект, уничтоженный вместе с окном посреди показа, пропускается.</summary>
        public void Return(T item)
        {
            if (item == null)
                return;

            item.gameObject.SetActive(false);
            item.transform.SetParent(Root, false);
            _free.Push(item);
        }

        private Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("UiPool " + typeof(T).Name);
                    go.SetActive(false);
                    _root = go.transform;
                }

                return _root;
            }
        }
    }
}
