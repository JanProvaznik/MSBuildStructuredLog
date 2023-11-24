﻿namespace Microsoft.Build.Logging.StructuredLogger
{
    public class TextNode : TreeNode
    {
        public string Text { get; set; }
        public string ShortenedText => TextUtilities.ShortenValue(Text);
        public bool IsTextShortened => Text != ShortenedText;

        public override string TypeName => nameof(TextNode);
        public override string Title => Text;

        public override string ToString()
        {
            var baseText = base.ToString();
            if (string.IsNullOrEmpty(baseText))
            {
                return Text;
            }

            return baseText + " " + Text;
        }
    }
}
