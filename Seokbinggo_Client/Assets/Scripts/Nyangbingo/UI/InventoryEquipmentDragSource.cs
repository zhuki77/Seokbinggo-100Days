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
        private Vector2 lastPosition;

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
            lastPosition = eventData.position;
            begin?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData != null) lastPosition = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            if (!dragging) return;
            dragging = false;
            if (eventData.position == Vector2.zero) eventData.position = lastPosition;
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

    /// <summary>EventTrigger 없이 장비 칸이 드래그 드롭을 직접 받는다.</summary>
    public sealed class EquipmentSlotDropTarget : MonoBehaviour, IDropHandler
    {
        private Action<PointerEventData> dropped;

        public void Configure(Action<PointerEventData> onDrop) => dropped = onDrop;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            dropped?.Invoke(eventData);
        }
    }
}
