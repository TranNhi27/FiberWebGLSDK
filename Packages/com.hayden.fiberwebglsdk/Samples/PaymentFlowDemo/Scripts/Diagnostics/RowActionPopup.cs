using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FiberWebGLSDK.Samples
{
    /// <summary>
    /// A small modal presenting one or more labelled actions for a selected list row.
    /// Reusable across row types: a peer row shows a single action, a channel row shows
    /// several. Actions are supplied at call time via Show(); the popup builds one button
    /// per action and dismisses itself once one is chosen or the backdrop is tapped.
    /// </summary>
    public class RowActionPopup : MonoBehaviour
    {
        /// <summary>A single choice in the popup: a button label and what it does.</summary>
        public readonly struct Action
        {
            public readonly string Label;
            public readonly System.Action Callback;

            public Action(string label, System.Action callback)
            {
                Label = label;
                Callback = callback;
            }
        }

        [Header("Structure")]
        [Tooltip("The dimmed full-screen backdrop; tapping it dismisses the popup.")]
        [SerializeField] private Button backdropButton;
        [Tooltip("Parent the action buttons are spawned under.")]
        [SerializeField] private Transform actionButtonParent;
        [SerializeField] private Button actionButtonPrefab;

        [Header("Optional")]
        [SerializeField] private TMP_Text titleText;

        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (backdropButton != null)
                backdropButton.onClick.AddListener(Hide);

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (backdropButton != null)
                backdropButton.onClick.RemoveListener(Hide);
        }

        /// <summary>
        /// Opens the popup with the given title and actions. Each action becomes a button;
        /// choosing one runs its callback and closes the popup.
        /// </summary>
        public void Show(string title, IReadOnlyList<Action> actions)
        {
            if (titleText != null)
                titleText.text = title;

            ClearButtons();

            foreach (var action in actions)
            {
                var button = Instantiate(actionButtonPrefab, actionButtonParent);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = action.Label;

                // Capture the callback for this specific button. Without a local copy the
                // closure would capture the loop variable and every button would run the
                // last action.
                var captured = action.Callback;
                button.onClick.AddListener(() =>
                {
                    Hide();
                    captured?.Invoke();
                });

                _spawnedButtons.Add(button.gameObject);
            }

            gameObject.SetActive(true);
        }

        /// <summary>Closes the popup without choosing an action.</summary>
        public void Hide()
        {
            ClearButtons();
            gameObject.SetActive(false);
        }

        private void ClearButtons()
        {
            foreach (var button in _spawnedButtons)
                Destroy(button);
            _spawnedButtons.Clear();
        }
    }
}