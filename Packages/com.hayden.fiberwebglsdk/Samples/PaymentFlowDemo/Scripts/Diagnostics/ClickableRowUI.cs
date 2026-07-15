using UnityEngine;
using UnityEngine.EventSystems;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// Base for a list row that raises an action request when clicked. Subclasses
    /// bind their own data and define which actions the row offers.
    ///
    /// Requires a Graphic (e.g. an Image, even transparent) on the same GameObject to
    /// receive pointer events, and a GraphicRaycaster on the parent Canvas.
    /// </summary>
    public abstract class ClickableRowUI : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            OnRowClicked();
        }

        /// <summary>Invoked when the row is clicked. Subclasses raise their action request here.</summary>
        protected abstract void OnRowClicked();
    }
}