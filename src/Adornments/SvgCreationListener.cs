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
    [ContentType("svg")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class SvgAdornmentProvider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            CreateAdornmentAsync(textView).ConfigureAwait(false);
        }

        private async Task CreateAdornmentAsync(IWpfTextView textView)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                textView.Properties.GetOrCreateSingletonProperty(() => new SvgAdornment(textView));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }
    }
}
