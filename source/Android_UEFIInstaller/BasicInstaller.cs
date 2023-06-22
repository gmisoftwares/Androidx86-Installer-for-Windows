using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Android_UEFIInstaller
{
    enum InstallationStep
    {
        CREATE_DIRECTORIES,
        EXTRACT_ISO,
        EXTRACT_SFS,
        CREATE_DATA,
        FORMAT_DATA,
        INSTALL_BOOT,
        /*
        MOUNT_EFI_PARTITION,
        INSTALL_BOOT,
        UEFI_ENTRY,
         */
        REVERT_ALL
        
    }
    abstract class BasicInstaller
    {
        PrivilegeClass.Privilege FirmwarePrivilege;
        public BasicInstaller()
        {
            FirmwarePrivilege = new PrivilegeClass.Privilege("SeSystemEnvironmentPrivilege");
        }

        public virtual bool Install(string ISOFilePath, string InstallDrive, string UserDataSize,string isRoot)
        {
            string InstallDirectory = string.Format(config.INSTALL_DIR, InstallDrive);
            Log.write(string.Format("====Install Started on {0}====", DateTime.Now));
            Log.write("-ISO File: " + ISOFilePath);
            Log.write("-TargetDrive: " + InstallDrive);
            Log.write("-UserData: " + UserDataSize);

         
            string OtherInstall = SearchForPreviousInstallation(config.INSTALL_FOLDER);
            if ( OtherInstall != "0")
            {
                Log.write("Another Installation found on: " + OtherInstall + @":\");
                return false;
            }

            if (!SetupDirectories(InstallDirectory))
                return false;

            if (!ExtractISO(ISOFilePath, InstallDirectory))
                goto cleanup;

            setVersionTag(ISOFilePath, InstallDirectory);
           
            /*
             * System.sfs found extract it
             * System.sfs included in Androidx86 dist and not found with RemixOS
             */

            string[] FileList = {InstallDirectory + @"\kernel",
                                InstallDirectory + @"\initrd.img",
                                InstallDirectory + @"\system.sfs"
                                }; //InstallDirectory + @"\gearlock",      Optional

            string achvFile = "system.sfs";
            if (File.Exists(InstallDirectory + @"\system.efs"))
            {
                achvFile = "system.efs";
                FileList[2] = InstallDirectory + @"\system.efs";
            };

            if (isRoot == "true")
            {                
                if (ExtractSFS(achvFile, InstallDirectory))
                   FileList[2] = InstallDirectory + @"\system.img"; 
                else   
                   goto cleanup;                
            }

                     
            if (!VerifyFiles(FileList))
                goto cleanup;

            //if(!DetectAndroidVariant(ISOFilePath,InstallDirectory))
            //    goto cleanup;  
  
            if (!CreateDataParition(InstallDirectory, UserDataSize))
                goto cleanup;

            if (!FormatDataPartition(InstallDirectory))
                goto cleanup;

            if (!WriteAndroidIDFile(InstallDirectory))
                goto cleanup;

            if (!InstallBootObjects(ISOFilePath))
                goto cleanup;

          
            Log.write("==========================================");
            Log.updateStatus("Installation finished!");
            return true;

        cleanup:
            Log.updateStatus("Installation failed! Rolling back");
            Log.write("==============Revert Installation==============");
            cleanup(InstallDirectory);
            UnInstallBootObjects(null);
            Log.write("==========================================");
            Log.updateStatus("Nothing happend");
            return false;
        }

        public void Uninstall(string InstallDrive="0")
        {
            string InstallDirectory = string.Format(config.INSTALL_DIR, InstallDrive);

            Log.write(string.Format("====Uninstall Started on {0}====", DateTime.Now));

            InstallDrive = SearchForPreviousInstallation(config.INSTALL_FOLDER);
            if (InstallDrive != "0")
            {
                cleanup(string.Format(config.INSTALL_DIR,InstallDrive));
            }
            else
            {
                Log.write("Android Installation Not Found");
            }
            FirmwarePrivilege.Enable();
            UnInstallBootObjects(null);
            FirmwarePrivilege.Revert();
            Log.updateStatus("Cleanup complete!");
            Log.write("==========================================");
        }

        string SearchForPreviousInstallation(string FolderName)
        {
            string[] drives = Environment.GetLogicalDrives();

            foreach (string drive in drives)
            {
                if (Directory.Exists(drive + FolderName))
                {
                    return drive.Substring(0, 1);
                }
            }

            return "0";
        }

        private bool SetupDirectories(string directory)
        {
            Log.write("-Setup Directories...");
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.write("-Folder Created: " + directory);
                    return true;
                }
                else
                {
                    
                    Log.write(directory + " Already Exists");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.write("Error Creating OS folders:" + ex.Message.ToString() + "Dir:" + directory);
                return false;
            }
        }

        #region "ISO Extraction"
        private bool ExtractISO(string ISOFilePath, string ExtractDirectory)
        {
            //7z.exe x android-x86-4.4-r2.img "efi" "kernel" "gearlock" "initrd.img" "system.sfs" -o"C:\Users\ExtremeGTX\Desktop\installer_test\extracted\"
            string ExecutablePath = Environment.CurrentDirectory + @"\7z.exe";
            string ExecutableArgs = string.Format(" x \"{0}\" \"kernel\" \"gearlock\" \"initrd.img\" \"system.*\" -o{1}", ISOFilePath, ExtractDirectory);    //{0} ISO Filename, {1} extraction dir

            //
            //Extracting ISO Contents
            //
            Log.updateStatus("Status: Extracting ISO... Please wait");
            Log.write("-Extracting ISO");

            if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                    return false;
         
            return true;
        }

        private void setVersionTag(string ISOFilePath, string ExtractDirector)
        {
            string fileName = ISOFilePath.Split('\\').Last().Replace(".iso","");
          
            string[] parts = fileName.Split('-');

            string BUILD = parts.Last();    //20230614
            string ANDROIDVERS = parts[1];      //v15.8.6
            string BUILDTYPE = parts[4];        //gapps or foss

            string output = string.Format("{0};{1};{2}",BUILD,ANDROIDVERS,BUILDTYPE);
            
            Log.updateStatus("Status: Copying boot files...");
            File.WriteAllText(ExtractDirector + @"\tag.txt", output);
            //return File.Exists(ExtractDirector + @"\tag.txt");
        }

        private bool ExtractSFS(string fileToExtract,string SFSPath)
        {
            //7z.exe x android-x86-4.4-r2.img "efi" "kernel" "initrd.img" "system.sfs" -o"C:\Users\ExtremeGTX\Desktop\installer_test\extracted\"
     
            string ExecutablePath = Environment.CurrentDirectory + @"\7z.exe"; //version 16.02
            string ExecutableArgs = string.Format(" x {0}\\system.sfs \"system.img\" -o{0}", SFSPath);
            
            //Extracting System.sfs
            Log.updateStatus("Status: Extracting SFS... Please wait");
            Log.write("-Extracting SFS");

            if (fileToExtract == "system.efs")
            {
                ExecutablePath = Environment.CurrentDirectory + @"\efs\extract.erofs.exe";
                ExecutableArgs = string.Format(" -x -i {0}\\system.efs -o{0}", SFSPath);

                //Extracting System.sfs
                Log.updateStatus("Status: Extracting EFS... Please wait");
                Log.write("-Extracting EFS");
            }
                         
            if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                return false;

            Log.write("-Removing " + fileToExtract);
            string sysFile = string.Format(" {0}\\{1}", SFSPath,fileToExtract);
          
            File.Delete(sysFile);

            if (fileToExtract == "system.efs")
            {
                File.Move(SFSPath + @"\system\system.img", SFSPath + @"\system.img");
                Directory.Delete(SFSPath + @"\system", true);
                Directory.Delete(SFSPath + @"\config",true);
            }

            return true;
        }
        #endregion
        
        #region "Data Partition"
        private bool CreateDataParition(string directory, string Size)
        {
            
            Log.updateStatus("Status: Creating Data.img... Please wait");
            Log.write("-Creating Data.img");

            string ExecutablePath = Environment.CurrentDirectory + @"\dd.exe";
            string ExecutableArgs = string.Format(@"if=/dev/zero of={0}\data.img count={1}", directory, Size.ToString());

            if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                return false;

            return true;

        }

        private bool FormatDataPartition(string FilePath)
        {
            Log.updateStatus("Status: initialize Data.img... Please wait");
            Log.write("-Initialize Data.img");
            string ExecutablePath = Environment.CurrentDirectory + @"\mke2fs.exe";
            string ExecutableArgs = string.Format("-F -t ext4 \"{0}\\data.img\"", FilePath);

            if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                return false;

            return true;
        }
        #endregion

        private bool VerifyFiles(string[] FileList)
        {
            foreach (string file in FileList)
            {
                if (!File.Exists(file))
                {
                    Log.write("File: " + file + " not exist");
                    return false;
                }
            }

            return true;
        }

        private bool WriteAndroidIDFile(string filePath)
        {
            File.WriteAllText(filePath + @"\android.boot", "GRUB2_ANDROID_ID");
            return File.Exists(filePath + @"\android.boot");
        }

        private bool DetectAndroidVariant(string ISOFilePath, string ExtractDirectory)
        {
            //Extract grub.cfg
            //Check for androidboot.hardware value
            //Set config.remixos

            string ExecutablePath = Environment.CurrentDirectory + @"\7z.exe";
            string ExecutableArgs = string.Format(" e \"{0}\" \"boot\\grub\\grub.cfg\"  -o{1}", ISOFilePath, ExtractDirectory);

    
            Log.updateStatus("Status: Check Android variant type...");
            if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                return false;

            if (!File.Exists(ExtractDirectory + @"\grub.cfg"))
                return false;

            string grubcfg = File.ReadAllText(ExtractDirectory + @"\grub.cfg");

            int idx = grubcfg.IndexOf("remix");
            //if (idx <= 0){
            //    config.RemixOS_Found = false;
            //}
            //else
            //{
            //    Log.write("RemixOS Found");
            //    config.RemixOS_Found = true;
            //}

            File.Delete(ExtractDirectory + @"\grub.cfg");
            return true;

        }

        protected abstract bool InstallBootObjects(Object extraData);
        protected abstract bool UnInstallBootObjects(Object extraData);

        protected virtual bool cleanup(string directory)
        {
            Log.write("-Cleaning up Bliss Directory ... " + directory);
            try
            {
                //Check if Directory Exist
                if (Directory.Exists(directory))
                {
                   Directory.Delete(directory, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.write("Exception: " + ex.Message);
                return false;
            }

        }
        /*
        protected void revert(InstallationStep step, Object info)
        {
            switch (step)
            {
                case InstallationStep.REVERT_ALL:
                case InstallationStep.INSTALL_BOOT:
                    UnInstallBootObjects((int)info);
                    goto case InstallationStep.FORMAT_DATA;

                case InstallationStep.FORMAT_DATA:
                case InstallationStep.CREATE_DATA:
                case InstallationStep.EXTRACT_SFS:
                case InstallationStep.EXTRACT_ISO:
                    string iso = info as string;
                    //Log.write("Error: ISO Extraction failed > " + iso);
                    //Directory.Delete(InstallDirectory, true);
                   // Directory.EnumerateFileSystemEntries(InstallDirectory).Any();
                    break;

                case InstallationStep.CREATE_DIRECTORIES:
                    string dir = info as string;
                    Log.write("Error: Folder Exist > " + dir);
                    //System.Windows.MessageBox.Show(dir + " Already Exist\n" + "Aborting Installation Process", "Error", System.Windows.forms.MessageBoxButtons.OK);
                    break;

                default:
                    break;
            }
        }
        */
        protected bool ExecuteCLICommand(string FilePath, string args)
        {
            string CliExecutable = FilePath;
            string CliArguments = args;
            try
            {

                Process p = new Process();
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;

                Log.write("#Launch:" + CliExecutable + CliArguments);
                p.StartInfo.FileName = CliExecutable;
                p.StartInfo.Arguments = CliArguments;
                p.Start();
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    Log.write(string.Format("Error Executing {0} with Args: {1}", FilePath, args));
                    Log.write("Error output:");
                    Log.write(p.StandardError.ReadToEnd());
                    Log.write(p.StandardOutput.ReadToEnd());
                    return false;
                }
                         
                return true;
            }
            catch (Exception ex)
            {
                Log.write("Exception: " + ex.Message);
                return false;
            }
        }

        public virtual bool UpdateInstall(string ISOFilePath, string InstallDrive, string UserDataSize, string isRoot)
        {
            string InstallDirectory = string.Format(config.INSTALL_DIR, InstallDrive);
            Log.write(string.Format("====Install Started on {0}====", DateTime.Now));
            Log.write("-ISO File: " + ISOFilePath);
            Log.write("-TargetDrive: " + InstallDrive);
            Log.write("-UserData: " + UserDataSize);

            if (!ExtractISO(ISOFilePath, InstallDirectory))
                goto cleanup;

            string[] FileList = {InstallDirectory + @"\kernel",
                                InstallDirectory + @"\initrd.img",
                                InstallDirectory + @"\system.sfs"
                                }; //InstallDirectory + @"\gearlock",      Optional

            string achvFile = "system.sfs";
            if (File.Exists(InstallDirectory + @"\system.efs"))
            {
                achvFile = "system.efs";
                FileList[2] = InstallDirectory + @"\system.efs";
            };

            if (isRoot == "true")
            {
                if (ExtractSFS(achvFile, InstallDirectory))
                    FileList[2] = InstallDirectory + @"\system.img";
                else
                    goto cleanup;
            }

            if (!VerifyFiles(FileList))
                goto cleanup;

            if (!InstallBootObjects(ISOFilePath))
                goto cleanup;

            setVersionTag(ISOFilePath, InstallDirectory);

            Log.write("==========================================");
            Log.updateStatus("Installation finished!");
            return true;

            cleanup:
            Log.updateStatus("Installation failed!");
            //Log.write("==============Revert Installation==============");
            //cleanup(InstallDirectory);
            //UnInstallBootObjects(null);
            //Log.write("==========================================");
            //Log.updateStatus("Nothing happend");
            return false;
        }

    }
}
