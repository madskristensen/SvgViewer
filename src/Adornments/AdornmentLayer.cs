using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace SvgViewer
{
    class AdornmentLayer
    {
        public const string LayerName = Vsix.Name;

        [Export(typeof(AdornmentLayerDefinition))]
        [Name(LayerName)]
        [Order(After = PredefinedAdornmentLayers.Text)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;
    }
}
