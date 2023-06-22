﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Android_UEFIInstaller
{
    /// <summary>
    /// Interaction logic for AndroidMaterial.xaml
    /// </summary>
    public partial class AndroidMaterial : Window
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        WindowsSecurity ws = new WindowsSecurity();
        IntPtr Handle;
        BackgroundWorker InstallationTask;

        string rootOn = "false";
        string task = "install";

        public AndroidMaterial()
        {
            InitializeComponent();
            cmdUpdate.IsEnabled = false;

            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            this.Title += "v" + v.Major.ToString() + "." + v.Minor.ToString();
#if ALPHA_TRIAL
            DateTime d = new DateTime(2015, 11, 6);
            if (d <= DateTime.Today)
            { 
                MessageBox.Show("This is an expired alpha testing version\nPlease check for the latest release, Application will exit ");
                Environment.Exit(0);
            }
#endif
            //
            //Update Version
            //
            txtVersion.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            //
            //Setup TxtLog for logging
            //
            Log.SetLogBuffer(txtlog);
            Log.SetStatuslabel(lblStatus);
            //
            //SetupGlobalExceptionHandler
            //
            SetupGlobalExceptionHandler();
            //
            //Log Some Info
            //
            Log.write("================Installer Info================");
            Log.write("Installer Directory:" + Environment.CurrentDirectory);
            Log.write("Installer Version:" + System.Reflection.Assembly.GetExecutingAssembly()
                                            .GetName()
                                            .Version
                                            .ToString());
            //
            // Machine Info
            //
            GetMachineInfo();
            //
            // Check if Requirements satisifed
            //
            if (!RequirementsCheck())
            {
                DisableUI();
                MessageBox.Show("Not all system requirements are met\nPlease check the installer log");
            }

            Log.write("==========================================");
        }

        public void SetupGlobalExceptionHandler()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(MyHandler);
            AppDomain.CurrentDomain.ProcessExit += currentDomain_ProcessExit;

        }

        void currentDomain_ProcessExit(object sender, EventArgs e)
        {
            FreeLibrary(Handle);
            Log.save();
        }

        static void MyHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Log.write("MyHandler caught : " + e.Message);
            Log.write(string.Format("Runtime terminating: {0}", args.IsTerminating));
            Log.save();
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static bool IsCPU64bit()
        {
            //
            // Machine Info
            //
            ManagementObjectSearcher objOSDetails = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            ManagementObjectCollection osDetailsCollection = objOSDetails.Get();

            foreach (ManagementObject mo in osDetailsCollection)
            {
                Log.write("CPU Architecture: " + mo["Architecture"].ToString());
                Log.write("CPU Name: " + mo["Name"].ToString());

                UInt16 Arch = UInt16.Parse(mo["Architecture"].ToString());
                if (Arch == 9) //x64
                {
                    return true;
                }
            }
            return false;
        }

        void GetMachineInfo()
        {
            //
            // SecureBoot Status
            //
            RegistryKey Subkey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (Subkey != null)
            {
                int val = (int)Subkey.GetValue("UEFISecureBootEnabled");
                if (val == 0)
                {
                    Log.write("Secure Boot ... Disabled");
                }
                else
                {
                    Log.write("Secure Boot ... Enabled");
                }
            }
            else
            {
                Log.write("Secure Boot ... Not Supported");
            }

            //
            // Machine Info
            //
            ManagementObjectSearcher objOSDetails = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            ManagementObjectCollection osDetailsCollection = objOSDetails.Get();

            foreach (ManagementObject mo in osDetailsCollection)
            {
                Log.write("Manufacturer: " + mo["Manufacturer"].ToString());
                Log.write("Model: " + mo["Model"].ToString());
            }

            //
            // Motherboard Model
            //
            objOSDetails.Query = new ObjectQuery("SELECT * FROM Win32_BaseBoard");
            osDetailsCollection = objOSDetails.Get();
            foreach (ManagementObject mo in osDetailsCollection)
            {
                Log.write("Product: " + mo["Product"].ToString());
            }

            //
            // BIOS Version
            //
            objOSDetails.Query = new ObjectQuery("SELECT * FROM Win32_BIOS");
            osDetailsCollection = objOSDetails.Get();
            foreach (ManagementObject mo in osDetailsCollection)
            {
                string[] iBIOS = (string[])mo["BIOSVersion"];
                Log.write("BIOS info:");
                foreach (string item in iBIOS)
                {
                    Log.write(item);
                }
            }

            //
            // Graphics Card type
            //
            objOSDetails.Query = new ObjectQuery("SELECT * FROM Win32_VideoController");
            osDetailsCollection = objOSDetails.Get();
            Log.write("Available GPU(s):");
            foreach (ManagementObject mo in osDetailsCollection)
            {
                Log.write("GPU: " + mo["Description"].ToString());
            }
        }
        bool RequirementsCheck()
        {
            /*
             * App is running as admin
             * Access to NVRAM Granted
             * System has UEFI
             * System is running Windows 8 or higher
             * System is running on Windows 64-bit 
             * Target partition has enough space
             * 
             */
            Log.write("=============[REQUIREMENTS CHECK]============");
            //
            //Administrator check
            //
            if (IsAdministrator())
                Log.write("Administrator privilege ... ok");
            else
            {
                Log.write("Administrator privilege ... fail");
                return false;
            }
            //
            // 64-bit check
            //
            if (!Environment.Is64BitOperatingSystem)
            {
                Log.write("OS Type: 32-bit!");
            }
            //
            // Check if CPU Arch. is 64-bit
            //
            if (!IsCPU64bit())
            {
                Log.write("CPU Architecture is not supported!");
                return false;
            }
            //
            // OS Version Check
            //
            Log.write("OSVer: " + Environment.OSVersion.ToString());
            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                switch (System.Environment.OSVersion.Version.Major)
                {
                    case 6:
                        if (System.Environment.OSVersion.Version.Minor >= 2)
                            Log.write("OperatingSystem Version ... ok");
                        break;
                    case 10:
                        Log.write("OperatingSystem Version ... ok");
                        break;
                    default:
                        return false;
                }
            }
            else
                return false;

            //
            //Load UEFI Library
            //
            Handle = LoadLibrary(@"Win32UEFI.dll");
            if (Handle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Log.write(string.Format("Failed to load library (ErrorCode: {0})", errorCode));
                return false;
            }

            //
            //NVRAM Access
            //            
            if (ws.GetAccesstoNVRam())
                Log.write("Windows Security: Access NVRAM Privilege ... ok");
            else
            {
                Log.write("Windows Security: Access NVRAM Privilege ... Not All Set");
            }

            //
            //UEFI Check
            //
            if (UEFIWrapper.UEFI_isUEFIAvailable())
                Log.write("System Firmware: UEFI");
            else
            {
                Log.write("System Firmware: Other");
                return false;
            }

            return true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string Path = txtISOPath.Text;
            string Drive = cboDrives.Text.Substring(0, 1);
            string Size = Convert.ToUInt64((sldrSize.Value * 1024 * 1024 * 1024) / 512).ToString();

            if (!File.Exists(Path))
            {
                MessageBox.Show("Android IMG File is not exist");
                return;
            }
            if (Size == "0")
            {
                MessageBox.Show("Data Size is not set");
                return;
            }

            InstallationTask = new BackgroundWorker();
            InstallationTask.WorkerReportsProgress = false;
            InstallationTask.DoWork += InstallationTask_DoWork;
            InstallationTask.ProgressChanged += InstallationTask_ProgressChanged;
            InstallationTask.RunWorkerCompleted += InstallationTask_RunWorkerCompleted;

            DisableUI();
            pbarStatus.IsIndeterminate = true;

            string[] InstallInfo = { Path, Drive, Size };
            InstallationTask.RunWorkerAsync(InstallInfo);
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            task = "update";
            
            Button_Click(sender, e);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (MessageBoxResult.No == MessageBox.Show("Are you sure you want to remove android ?", "Android Installer", MessageBoxButton.YesNo, MessageBoxImage.Question))
                return;

            DisableUI();
            UEFIInstaller u = new UEFIInstaller();
            u.Uninstall();
            MessageBox.Show("Uninstall Done");
            EnableUI();
        }

        //private void Button_Click_2(object sender, RoutedEventArgs e)
        //{
        //    OpenFileDialog dlg = new OpenFileDialog();
        //    dlg.DefaultExt = ".img";
        //    dlg.Filter = "Android System Image |*.iso;*.img";

        //    if (dlg.ShowDialog() == true)
        //    {
        //        txtISOPath.Text = dlg.FileName;
        //        cmdInstall.IsEnabled = true;

        //        string Drive = cboDrives.Text.Substring(0, 1);
        //        string InstallDirectory = string.Format(config.INSTALL_DIR, Drive);
                             
        //    }
        //}

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (string item in Environment.GetLogicalDrives())
            {
                cboDrives.Items.Add(item);
            }

            cboDrives.SelectedIndex = 0;
        }

        void DisableUI()
        {
            cmdInstall.IsEnabled = false;
            cmdRemove.IsEnabled = false;
            cboDrives.IsEnabled = false;
            sldrSize.IsEnabled = false;
            ImgCmdBrowse.IsEnabled = false;
            tgRoot.IsEnabled = false;
        }

        void EnableUI()
        {
            cmdInstall.IsEnabled = true;
            cmdRemove.IsEnabled = true;
            cboDrives.IsEnabled = true;
            sldrSize.IsEnabled = true;
            ImgCmdBrowse.IsEnabled = true;
            tgRoot.IsEnabled = true;
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = ".img";
            dlg.Filter = "Android System Image |*.iso;*.img";

            if (dlg.ShowDialog() == true)
            {
                txtISOPath.Text = dlg.FileName;
                cmdInstall.IsEnabled = true;

                string Drive = cboDrives.Text.Substring(0, 1);
                string InstallDirectory = string.Format(config.INSTALL_DIR, Drive);

                cmdUpdate.IsEnabled = checkUpdate(txtISOPath.Text, InstallDirectory);
            }
        }

        private void cboDrives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            long DiskSize = GetTotalFreeSpace(cboDrives.SelectedItem.ToString());

            sldrSize.Maximum = ((DiskSize - config.ANDROID_SYSTEM_SIZE) / 1024 / 1024 / 1024);
            sldrSize.Value = 0.1 * sldrSize.Maximum;
            sldrSize.TickFrequency = 0.01 * sldrSize.Maximum;
        }

        private void txtlog_TextChanged(object sender, TextChangedEventArgs e)
        {

            txtlog.ScrollToEnd();
        }

        private long GetTotalFreeSpace(string driveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name == driveName)
                {
                    return drive.AvailableFreeSpace;
                }
            }
            return -1;
        }

        void InstallationTask_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] InstallInfo = (string[])e.Argument;
            string Path = InstallInfo[0];
            string Drive = InstallInfo[1];
            string Size = InstallInfo[2];

            UEFIInstaller u = new UEFIInstaller();

            if (task == "update")
            {
                if (!u.UpdateInstall(Path, Drive, Size, rootOn))
                    MessageBox.Show("Update Failed" + Environment.NewLine + "Please check log at C:\\AndroidInstall_XXX.log");
                else
                    MessageBox.Show("Update Done");
            }
            else
            {
                if (!u.Install(Path, Drive, Size, rootOn))
                    MessageBox.Show("Install Failed" + Environment.NewLine + "Please check log at C:\\AndroidInstall_XXX.log");
                else
                    MessageBox.Show("Install Done");
            }

            task = "install";
            MessageBox.Show("Kindly report back the installation status to the developer");
        }

        void InstallationTask_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        void InstallationTask_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            pbarStatus.IsIndeterminate = false;
            EnableUI();
        }

        private void toggleButton_onCheckChanged(object sender, RoutedEventArgs e)
        {
            if (tgRoot.IsChecked == true)
            {
                rootOn = "true";
            }
            else
            {
                rootOn = "false";
            }

            Log.write("root = " + rootOn);
        }

        private bool checkUpdate(string ISOFilePath, string ExtractDirector)
        {
            Log.updateStatus("Status: checking for update...");

            try
            {

                string fileName = ISOFilePath.Split('\\').Last().Replace(".iso", "");

                string[] parts = fileName.Split('-');

                string ANDROIDVERS = parts[1];      //v15.8.6
                string BUILDTYPE = parts[4];        //gapps or foss
                string BUILDDATE = parts.Last();    //20230614

                if (!File.Exists(ExtractDirector + "\\tag.txt"))
                {
                    return false;
                }

                string output = File.ReadAllText(ExtractDirector + "\\tag.txt");
                string[] outs = output.Split(';');
                string builddate = outs[0];
                string androidvers = outs[1];
                string buildtype =outs.Last(); 

                if (buildtype == BUILDTYPE)
                {
                    ANDROIDVERS = ANDROIDVERS.Split('.').First().Replace("v", "");
                    androidvers = androidvers.Split('.').First().Replace("v", "");
                    int VERS = int.Parse(ANDROIDVERS);
                    int vers = int.Parse(androidvers);

                    int bdate = int.Parse(builddate);
                    int DATE = int.Parse(BUILDDATE);

                    Log.updateStatus("Status: Ready...");

                    if (vers > VERS)
                    {
                        return true;
                    }

                    if ((vers == VERS) && (bdate > DATE))
                    {
                        return true;
                    }

                }

            }
            catch (Exception ex)
            {
                Log.write("Exception: " + ex.Message);
            }

            Log.updateStatus("Status: Ready...");
            return false;
        }

    }
}
