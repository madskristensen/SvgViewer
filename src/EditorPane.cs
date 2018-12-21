using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace SvgViewer
{
    [ComVisible(true)]
    public sealed class EditorPane : WindowPane, IVsDeferredDocView
    {
        private readonly string _filePath;
        private readonly IVsTextLines _textBuffer;
        private BrowserView _browserView;

        public EditorPane(Package package, string filePath, IVsTextLines textBuffer)
            : base(null)
        {
            _filePath = filePath;
            _textBuffer = textBuffer;
        }

        protected override void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            base.Initialize();

            _browserView = new BrowserView(_filePath);

            Content = _browserView.Browser;

            RegisterIndependentView(true);

            if (GetService(typeof(IMenuCommandService)) is IMenuCommandService mcs)
            {
                AddCommand(mcs, VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.NewWindow,
                                new EventHandler(OnNewWindow), new EventHandler(OnQueryNewWindow));
                AddCommand(mcs, VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewCode,
                                new EventHandler(OnViewCode), new EventHandler(OnQueryViewCode));
            }
        }

        protected override void OnClose()
        {
            base.OnClose();
            _browserView.Dispose();
        }

        void RegisterIndependentView(bool subscribe)
        {
            var textManager = (IVsTextManager)GetService(typeof(SVsTextManager));

            if (textManager != null)
            {
                if (subscribe)
                {
                    textManager.RegisterIndependentView(this, _textBuffer);
                }
                else
                {
                    textManager.UnregisterIndependentView(this, _textBuffer);
                }
            }
        }

        #region Commands

        private static void AddCommand(IMenuCommandService mcs, Guid menuGroup, int cmdID,
                                   EventHandler commandEvent, EventHandler queryEvent)
        {
            // Create the OleMenuCommand from the menu group, command ID, and command event
            var menuCommandID = new CommandID(menuGroup, cmdID);
            var command = new OleMenuCommand(commandEvent, menuCommandID);

            // Add an event handler to BeforeQueryStatus if one was passed in
            if (null != queryEvent)
            {
                command.BeforeQueryStatus += queryEvent;
            }

            // Add the command using our IMenuCommandService instance
            mcs.AddCommand(command);
        }

        private void OnQueryNewWindow(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnQueryViewCode(object sender, EventArgs e)
        {
            var command = (OleMenuCommand)sender;
            command.Enabled = true;
        }

        private void OnNewWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var uishellOpenDocument = (IVsUIShellOpenDocument)GetService(typeof(SVsUIShellOpenDocument));

            if (uishellOpenDocument != null)
            {
                var windowFrameOrig = (IVsWindowFrame)GetService(typeof(SVsWindowFrame));
                if (windowFrameOrig != null)
                {
                    Guid LOGVIEWID_Primary = Guid.Empty;
                    int hr = uishellOpenDocument.OpenCopyOfStandardEditor(windowFrameOrig, ref LOGVIEWID_Primary, out IVsWindowFrame windowFrameNew);

                    if (windowFrameNew != null)
                    {
                        hr = windowFrameNew.Show();
                    }

                    ErrorHandler.ThrowOnFailure(hr);
                }
            }
        }

        private void OnViewCode(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var XmlTextEditorGuid = new Guid("FA3CD31E-987B-443A-9B81-186104E8DAC1");

            // Open the referenced document using our editor.
            VsShellUtilities.OpenDocumentWithSpecificEditor(this, _filePath, XmlTextEditorGuid, VSConstants.LOGVIEWID_Primary, out _, out _, out IVsWindowFrame frame);

            ErrorHandler.ThrowOnFailure(frame.Show());
        }

        #endregion

        #region IVsDeferredDocView
        
        int IVsDeferredDocView.get_CmdUIGuid(out Guid pGuidCmdId)
        {
            pGuidCmdId = PackageGuids.guidEditorFactory;
            return VSConstants.S_OK;
        }

        [EnvironmentPermission(SecurityAction.Demand)]
        int IVsDeferredDocView.get_DocView(out IntPtr ppUnkDocView)
        {
            ppUnkDocView = Marshal.GetIUnknownForObject(this);
            return VSConstants.S_OK;
        }

        #endregion
    }
}
