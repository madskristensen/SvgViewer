using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [Guid(PackageGuids.guidPackageString)]

    [ProvideXmlEditorChooserDesignerView("SVG", "svg", LogicalViewID.Primary, 0x60,
        DesignerLogicalViewEditor = typeof(EditorFactory),
        Namespace = "http://www.w3.org/2000/svg",
        MatchExtensionAndNamespace = false)]
    [ProvideEditorExtension(typeof(EditorFactory), EditorFactory.Extension, 0x40, NameResourceID = 106)]
    [ProvideEditorLogicalView(typeof(EditorFactory), LogicalViewID.Designer)]

    [ProvideUIContextRule(PackageGuids.guidIsSvgFileString,
    name: "Supported Files",
    expression: "SVG",
    termNames: new[] { "SVG" },
    termValues: new[] { "HierSingleSelectionName:" + EditorFactory.Extension + "$" })]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class SvgViewerPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            RegisterEditorFactory(new EditorFactory(this));

            await Commands.ViewCode.InitializeAsync(this);
            await Commands.ViewImage.InitializeAsync(this);
        }
    }
}
