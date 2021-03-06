using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using Growl.CoreLibrary;

namespace GrowlExtras.VisualStudioAddIn
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
        //private const string ENABLED_CAPTION = "Growl Add-In";
        //private const string DISABLED_CAPTION = "Growl Add-In (disabled)";
        //private const int ENABLED_FACEID = 1087;
        //private const int DISABLED_FACEID = 3265;
        //private bool enabled = true;

        BuildEventsClass bec;

        byte[] appIcon;
        byte[] successIcon;
        byte[] failureIcon;

        private Growl.Connector.GrowlConnector growl;
        private Growl.Connector.Application application;
        private Growl.Connector.NotificationType ntSolutionSuccess;
        private Growl.Connector.NotificationType ntSolutionFailed;
        private Growl.Connector.NotificationType ntProjectSuccess;
        private Growl.Connector.NotificationType ntProjectFailed;
        private ConfigForm cf;

		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
		}

        void BuildEvents_OnBuildDone(vsBuildScope Scope, vsBuildAction Action)
        {
            string solutionName = System.IO.Path.GetFileNameWithoutExtension(this._applicationObject.Solution.FullName);
            bool success = (this._applicationObject.Solution.SolutionBuild.LastBuildInfo == 0);

            // use another thread so that we dont slow the build down
            BuildInfo info = new BuildInfo(solutionName, success, true);
            System.Threading.ParameterizedThreadStart pts = new System.Threading.ParameterizedThreadStart(SendNotification);
            System.Threading.Thread t = new System.Threading.Thread(pts);
            t.Start(info);
        }

        void BuildEvents_OnBuildProjConfigDone(string Project, string ProjectConfig, string Platform, string SolutionConfig, bool Success)
        {
            if (!Properties.Settings.Default.SolutionOnly)
            {
                string name = GetProjectName(Project);

                // use another thread so that we dont slow the build down
                BuildInfo info = new BuildInfo(name, Success, false);
                System.Threading.ParameterizedThreadStart pts = new System.Threading.ParameterizedThreadStart(SendNotification);
                System.Threading.Thread t = new System.Threading.Thread(pts);
                t.Start(info);
            }
        }

        private string GetProjectName(string uniqueName)
        {
            string name = uniqueName;
            foreach (EnvDTE.Project proj in this._applicationObject.Solution.Projects)
            {
                if (proj.UniqueName == uniqueName)
                {
                    name = proj.Name;
                    break;
                }
            }
            return name;
        }

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;
			//if(connectMode == ext_ConnectMode.ext_cm_UISetup)
			//{
				object []contextGUIDS = new object[] { };
				Commands2 commands = (Commands2)_applicationObject.Commands;
				string toolsMenuName;

				try
				{
					//If you would like to move the command to a different menu, change the word "Tools" to the 
					//  English version of the menu. This code will take the culture, append on the name of the menu
					//  then add the command to that menu. You can find a list of all the top-level menus in the file
					//  CommandBar.resx.
                    ResourceManager resourceManager = new ResourceManager("GrowlExtras.VisualStudioAddIn.CommandBar", Assembly.GetExecutingAssembly());
					CultureInfo cultureInfo = new System.Globalization.CultureInfo(_applicationObject.LocaleID);
					string resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, "Tools");
					toolsMenuName = resourceManager.GetString(resourceName);
				}
				catch
				{
					//We tried to find a localized version of the word Tools, but one was not found.
					//  Default to the en-US word, which may work for the current culture.
					toolsMenuName = "Tools";
				}

				//Place the command on the tools menu.
				//Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
				Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

				//Find the Tools command bar on the MenuBar command bar:
				CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
				CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

				//This try/catch block can be duplicated if you wish to add multiple commands to be handled by your Add-in,
				//  just make sure you also update the QueryStatus/Exec method to include the new command names.
				try
				{
					//Add a command to the Commands collection:
                    //Command command = commands.AddNamedCommand2(_addInInstance, "ConfigureAddIn", ENABLED_CAPTION, "Configure Growl notifications for Visual Studio", true, ENABLED_FACEID, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);
                    Command command = commands.AddNamedCommand2(_addInInstance, "ConfigureAddIn", "Growl", "Configure Growl notifications for Visual Studio", true, 3159, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported + (int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

					//Add a control for the command to the tools menu:
					if((command != null) && (toolsPopup != null))
					{
						command.AddControl(toolsPopup.CommandBar, 1);
					}
				}
				catch(System.ArgumentException)
				{
					//If we are here, then the exception is probably because a command with that name
					//  already exists. If so there is no need to recreate the command and we can 
                    //  safely ignore the exception.
				}
			//}

            bec = _applicationObject.Events.BuildEvents as BuildEventsClass;

            InitializeAddIn();
		}

        private void InitializeAddIn()
        {
            this.cf = new ConfigForm();
            this.cf.PasswordChanged += new EventHandler(cf_PasswordChanged);

            this.appIcon = ConvertImageToBytes(global::GrowlExtras.VisualStudioAddIn.Properties.Resources.vs);
            this.successIcon = ConvertImageToBytes(global::GrowlExtras.VisualStudioAddIn.Properties.Resources.vs_success);
            this.failureIcon = ConvertImageToBytes(global::GrowlExtras.VisualStudioAddIn.Properties.Resources.vs_failed);

            //string password = Growl.Connector.GrowlConnector.TEST_PASSWORD; //TODO:
            //this.growl = new Growl.Connector.GrowlConnector(password);
            this.growl = new Growl.Connector.GrowlConnector(Properties.Settings.Default.Password);
            this.growl.EncryptionAlgorithm = Growl.Connector.Cryptography.SymmetricAlgorithmType.PlainText;

            this.application = new Growl.Connector.Application("Visual Studio");
            this.application.Icon = new Growl.CoreLibrary.BinaryData(this.appIcon);

            this.ntSolutionSuccess = new Growl.Connector.NotificationType("Solution Succeeded", "Solution Succeeded", new Growl.CoreLibrary.BinaryData(this.successIcon), true);
            this.ntSolutionFailed = new Growl.Connector.NotificationType("Solution Failed", "Solution Failed", new Growl.CoreLibrary.BinaryData(this.failureIcon), true);
            this.ntProjectSuccess = new Growl.Connector.NotificationType("Project Succeeded", "Project Succeeded", new Growl.CoreLibrary.BinaryData(this.successIcon), true);
            this.ntProjectFailed = new Growl.Connector.NotificationType("Project Failed", "Project Failed", new Growl.CoreLibrary.BinaryData(this.failureIcon), true);

            this.growl.Register(application, new Growl.Connector.NotificationType[] { ntSolutionSuccess, ntSolutionFailed, ntProjectSuccess, ntProjectFailed });

            this._applicationObject.Events.BuildEvents.OnBuildDone += new _dispBuildEvents_OnBuildDoneEventHandler(BuildEvents_OnBuildDone);
            this._applicationObject.Events.BuildEvents.OnBuildProjConfigDone += new _dispBuildEvents_OnBuildProjConfigDoneEventHandler(BuildEvents_OnBuildProjConfigDone);
        }

        void cf_PasswordChanged(object sender, EventArgs e)
        {
            this.growl.Password = Properties.Settings.Default.Password;
        }

        private byte[] ConvertImageToBytes(System.Drawing.Image image)
        {
            byte[] bytes = null;
            System.IO.MemoryStream ms = new System.IO.MemoryStream();
            using (ms)
            {
                image.Save(ms, image.RawFormat);
                byte[] iconBytes = new Byte[ms.Length - 1];
                ms.Position = 0;
                ms.Read(iconBytes, 0, iconBytes.Length);
                bytes = iconBytes;
            }
            return bytes;
        }

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}
		
		/// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
		/// <param term='commandName'>The name of the command to determine state for.</param>
		/// <param term='neededText'>Text that is needed for the command.</param>
		/// <param term='status'>The state of the command in the user interface.</param>
		/// <param term='commandText'>Text requested by the neededText parameter.</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if(neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
                if (commandName == "GrowlExtras.VisualStudioAddIn.Connect.ConfigureAddIn")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
			}
		}

		/// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
		/// <param term='commandName'>The name of the command to execute.</param>
		/// <param term='executeOption'>Describes how the command should be run.</param>
		/// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
		/// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
		/// <param term='handled'>Informs the caller if the command was handled or not.</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if(executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
                if (commandName == "GrowlExtras.VisualStudioAddIn.Connect.ConfigureAddIn")
				{
                    /* THIS IS ALL OLD
                    string toolsMenuName = "Tools";

                    //Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
                    Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

                    //Find the Tools command bar on the MenuBar command bar:
                    CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
                    CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

                    string currentCaption = (this.enabled ? ENABLED_CAPTION : DISABLED_CAPTION);
                    string newCaption = (this.enabled ? DISABLED_CAPTION : ENABLED_CAPTION);
                    int newID = (this.enabled ? DISABLED_FACEID : ENABLED_FACEID);

                    CommandBarControl cbc = null;
                    try
                    {
                        cbc = toolsPopup.Controls[currentCaption];
                    }
                    catch
                    {
                        cbc = toolsPopup.Controls[newCaption];
                    }

                    if (cbc != null)
                    {
                        CommandBarButton cbb = (CommandBarButton)cbc;
                        cbb.Caption = newCaption;
                        cbb.FaceId = newID;
                        this.enabled = !this.enabled;
                    }
                     * */

                    this.cf.Location = System.Windows.Forms.Control.MousePosition;
                    this.cf.ShowDialog();

					handled = true;
					return;
				}
			}
		}
		private DTE2 _applicationObject;
		private AddIn _addInInstance;


        private void SendNotification(object info)
        {
            BuildInfo buildInfo = (BuildInfo)info;

            if (Properties.Settings.Default.EnableNotifications)
            {
                int index = buildInfo.Name.IndexOf('\\');
                string name = buildInfo.Name;
                if (index > 0) name = name.Substring(0, index);

                string title = String.Format("Build {0}", (buildInfo.Success ? "succeeded" : "failed"));
                string text = String.Format("{0} '{1}' {2}.", (buildInfo.IsSolution ? "Solution" : "Project"), name, (buildInfo.Success ? "was built successfully" : "failed to build"));
                byte[] iconBytes = (buildInfo.Success ? this.successIcon : this.failureIcon);
                Growl.CoreLibrary.BinaryData icon = new Growl.CoreLibrary.BinaryData(iconBytes);

                Growl.Connector.NotificationType nt = null;
                if(buildInfo.IsSolution)
                {
                    nt = (buildInfo.Success ? this.ntSolutionSuccess : this.ntSolutionFailed);
                }
                else
                {
                    nt = (buildInfo.Success ? this.ntProjectSuccess : this.ntProjectFailed);
                }

                Growl.Connector.Notification notification = new Growl.Connector.Notification(this.application.Name, nt.Name, String.Empty, title, text, icon, false, Growl.Connector.Priority.Normal, null);

                this.growl.Notify(notification);
            }
        }


        private class BuildInfo
        {
            public BuildInfo(string name, bool success, bool isSolution)
            {
                this.IsSolution = isSolution;
                this.Name = name;
                this.Success = success;
            }

            public bool IsSolution;
            public string Name;
            public bool Success;
        }
	}
}