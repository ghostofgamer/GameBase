using UnityEngine;

namespace Base.Inspector
{
    /// <summary>
    /// Для полей [SerializeReference]: в инспекторе появляется выпадающий список всех классов-наследников типа поля.
    /// Новый класс (эффект, правило, награда) появляется в списке сам, как только он написан.
    /// </summary>
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
