using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Nyangbingo.UI
{
    /// <summary>Keeps a real drag from also firing the existing click-to-pick-up action.</summary>
    public sealed class InventoryEquipmentDragSource : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Action<PointerEventData> begin;
        private Action end;
        private Action<PointerEventData> endDrag;
        private bool dragging;

        public void Configure(Action<PointerEventData> onBegin, Action onEnd,
            Action<PointerEventData> onEndDrag = null)
        {
            begin = onBegin;
            end = onEnd;
            endDrag = onEndDrag;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Even a rejected/material drag must not become an accidental pickup.
            eventData.eligibleForClick = false;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            begin?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData) { }

        public void OnEndDrag(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            if (!dragging) return;
            dragging = false;
            // 슬롯의 Drop보다 나중에 온다. 이미 장착됐으면 드래그가 꺼져 있어 아래는 빈 동작이다.
            endDrag?.Invoke(eventData);
            end?.Invoke();
        }

        private void OnDisable()
        {
            if (!dragging) return;
            dragging = false;
            end?.Invoke();
        }
    }
}
