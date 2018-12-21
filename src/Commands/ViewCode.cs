using System;
using System.ComponentModel.Design;
using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer.Commands
{
    public class ViewCode
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            Assumes.Present(commandService);

            var cmdId = new CommandID(PackageGuids.guidCommands, PackageIds.VIEW_CODE);
            var cmd = new MenuCommand((s, e) => Execute(package), cmdId)
            {
                Supported = false
            };

            commandService.AddCommand(cmd);
        }

        private static void Execute(Package package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ViewImage.GetSelectedItem() is ProjectItem item)
            {
                string filePath = item.FileNames[1];
                var XmlTextEditorGuid = new Guid("FA3CD31E-987B-443A-9B81-186104E8DAC1");

                VsShellUtilities.OpenDocumentWithSpecificEditor(package, filePath, XmlTextEditorGuid, VSConstants.LOGVIEWID_Primary, out _, out _, out IVsWindowFrame frame);
                frame.Show();
            }
        }
    }
}
