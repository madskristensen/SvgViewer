using System;
using System.ComponentModel.Composition;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(ContentTypes.HTML)]
    [ContentType(ContentTypes.Xml)]
    [ContentType("svg")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class SvgAdornmentProvider : IWpfTextViewCreationListener
    {
        public void TextViewCreated(IWpfTextView textView)
        {
            textView.Properties.GetOrCreateSingletonProperty(() => new SvgAdornment(textView));
        }
    }
}
