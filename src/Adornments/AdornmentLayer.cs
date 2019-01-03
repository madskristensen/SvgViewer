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
        [Order(Before = PredefinedAdornmentLayers.Caret)]
        public AdornmentLayerDefinition editorAdornmentLayer = null;
    }
}
