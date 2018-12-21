using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SvgViewer.Commands
{
    public class ViewImage
    {
        public static async Task InitializeAsync(AsyncPackage package)
        {
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            Assumes.Present(commandService);

            var cmdId = new CommandID(PackageGuids.guidCommands, PackageIds.VIEW_IMAGE);
            var cmd = new MenuCommand((s, e) => Execute(package), cmdId)
            {
                Supported = false
            };

            commandService.AddCommand(cmd);
        }

        private static void Execute(Package package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (GetSelectedItem() is ProjectItem item)
            {
                string filePath = item.FileNames[1];
                VsShellUtilities.OpenDocumentWithSpecificEditor(package, filePath, PackageGuids.guidEditorFactory, VSConstants.LOGVIEWID_Primary, out _, out _, out IVsWindowFrame frame);
                frame.Show();
            }
        }

        public static object GetSelectedItem()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object selectedObject = null;

            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));

            try
            {
                monitorSelection.GetCurrentSelection(out IntPtr hierarchyPointer,
                                                 out uint itemId,
                                                 out IVsMultiItemSelect multiItemSelect,
                                                 out IntPtr selectionContainerPointer);


                if (Marshal.GetTypedObjectForIUnknown(
                                                     hierarchyPointer,
                                                     typeof(IVsHierarchy)) is IVsHierarchy selectedHierarchy)
                {
                    ErrorHandler.ThrowOnFailure(selectedHierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out selectedObject));
                }

                Marshal.Release(hierarchyPointer);
                Marshal.Release(selectionContainerPointer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }

            return selectedObject;
        }
    }
}
