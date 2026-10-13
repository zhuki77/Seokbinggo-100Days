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
        private bool dragging;

        public void Configure(Action<PointerEventData> onBegin, Action onEnd)
        {
            begin = onBegin;
            end = onEnd;
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
            Finish();
        }

        private void OnDisable() => Finish();

        private void Finish()
        {
            if (!dragging) return;
            dragging = false;
            end?.Invoke();
        }
    }
}
