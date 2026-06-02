using UnityEngine.UIElements;

namespace UnityMCP.Editor.Window
{
    /// <summary>
    /// Base class for tab content controllers. Provides shared UI helpers
    /// (cards, rows, etc.) so individual tabs stay focused on their data.
    /// </summary>
    internal abstract class TabController
    {
        public abstract void Build(VisualElement container);

        // --- Layout helpers ---

        /// <summary>Creates a card container with a title.</summary>
        protected static VisualElement Card(string title, string subtitle = null, string count = null)
        {
            var card = new VisualElement();
            card.AddToClassList(WindowStyles.Card);

            var header = new VisualElement();
            header.AddToClassList(WindowStyles.CardHeader);

            var titleStack = new VisualElement();
            titleStack.style.flexGrow = 1;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList(WindowStyles.CardTitle);
            titleStack.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subLabel = new Label(subtitle);
                subLabel.AddToClassList(WindowStyles.CardSubtitle);
                titleStack.Add(subLabel);
            }

            header.Add(titleStack);

            if (!string.IsNullOrEmpty(count))
            {
                var countLabel = new Label(count);
                countLabel.AddToClassList(WindowStyles.CardCount);
                header.Add(countLabel);
            }

            card.Add(header);
            return card;
        }

        /// <summary>Creates a key-value row (label on left, value on right).</summary>
        protected static VisualElement KvRow(string label, string value = null, bool mono = false, string valueClass = null)
        {
            var row = new VisualElement();
            row.AddToClassList(WindowStyles.KvRow);

            var labelEl = new Label(label);
            labelEl.AddToClassList(WindowStyles.KvLabel);
            row.Add(labelEl);

            if (value != null)
            {
                var valueEl = new Label(value);
                valueEl.AddToClassList(WindowStyles.KvValue);
                if (mono) valueEl.AddToClassList(WindowStyles.KvValueMono);
                if (valueClass != null) valueEl.AddToClassList(valueClass);
                row.Add(valueEl);
            }

            return row;
        }

        /// <summary>Creates an actions row for buttons (add to card bottom).</summary>
        protected static VisualElement ActionsRow()
        {
            var actions = new VisualElement();
            actions.AddToClassList(WindowStyles.Actions);
            return actions;
        }
    }
}
