using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideOptionPage(typeof(GeneralOptions), "SVG Viewer", "General", 0, 0, true)]
    [ProvideProfile(typeof(GeneralOptions), "SVG Viewer", "General", 0, 0, true)]
    [Guid("058ea1e2-7ae5-486c-8888-ee1d7cc2b49b")]
    public sealed class SvgViewerPackage : AsyncPackage
    {
        public static GeneralOptions Options { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Options = (GeneralOptions)GetDialogPage(typeof(GeneralOptions));
        }
    }
}
