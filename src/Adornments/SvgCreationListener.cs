using System;
using System.ComponentModel.Composition;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace SvgViewer
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(ContentTypes.HTML)]
    [ContentType(ContentTypes.WebForms)]
    [ContentType(ContentTypes.Xml)]
    [ContentType("svg")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class SvgAdornmentProvider : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null;

        public void TextViewCreated(IWpfTextView textView)
        {
            if (!TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out ITextDocument doc))
            {
                return;
            }

            if (!doc.FilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _ = textView.Properties.GetOrCreateSingletonProperty(() => new SvgAdornment(textView));
        }
    }
}
