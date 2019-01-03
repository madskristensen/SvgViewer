using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("htmlx")]
    [ContentType("xml")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class SvgAdornmentProvider : IWpfTextViewCreationListener
    {
        [Import]
        private ITextDocumentFactoryService DocumentService { get; set; }

        public void TextViewCreated(IWpfTextView textView)
        {
            CreateAdornmentAsync(textView).ConfigureAwait(false);
        }

        private async Task CreateAdornmentAsync(IWpfTextView textView)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                if (!DocumentService.TryGetTextDocument(textView.TextBuffer, out ITextDocument doc))
                {
                    return;
                }

                if (!doc.FilePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                textView.Properties.GetOrCreateSingletonProperty(() => new SvgAdornment(textView, doc));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }
    }
}
