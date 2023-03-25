namespace FileSplitterService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SFLCSCFileInstall = new System.ServiceProcess.ServiceProcessInstaller();
            this.SFLCSCFileServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // SFLCSCFileInstall
            // 
            this.SFLCSCFileInstall.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.SFLCSCFileInstall.Password = null;
            this.SFLCSCFileInstall.Username = null;
            // 
            // SFLCSCFileServiceInstaller
            // 
            this.SFLCSCFileServiceInstaller.ServiceName = "SFL CSC File Splitter";
            this.SFLCSCFileServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.SFLCSCFileInstall,
            this.SFLCSCFileServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller SFLCSCFileInstall;
        private System.ServiceProcess.ServiceInstaller SFLCSCFileServiceInstaller;
    }
}