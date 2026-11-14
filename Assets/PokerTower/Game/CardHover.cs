using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PokerTower.Game
{
    /// <summary>마우스가 카드 위에 올라가고 내려갈 때 알려 줍니다 (카드 확대 보기용). 규칙과 무관한 표시 전용.</summary>
    public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> Changed;

        public void OnPointerEnter(PointerEventData eventData) { if (Changed != null) Changed(true); }
        public void OnPointerExit(PointerEventData eventData) { if (Changed != null) Changed(false); }
        private void OnDisable() { if (Changed != null) Changed(false); }
    }
}
