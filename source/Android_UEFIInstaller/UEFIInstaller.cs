using System;
using System.IO;

namespace Android_UEFIInstaller
{
    class UEFIInstaller : BasicInstaller
    {
        enum ErrorCodes
        {
            MOUNT_EFI_PARTITION,
            INSTALL_BOOT,
            UEFI_ENTRY
        }
        
         /* 
             * 
             * Create dirs
             * Install Android Files
             * Make data.img
             * Install Boot files
             * Create UEFI Entry
             * cleanup temp data
             * 
             * #####
             * Folders:
             *           :\Android
             *          Z:\EFI\Android
             * Files:
             *           :\Android\kernel    
             *           :\Android\initrd    
             *           :\Android\gearlock
             *           :\Android\system.img
             *           :\Android\data.img
             *           
             * 
             *           :\EFI\Android\grubx64.efi
             *           :\EFI\Android\grub.cfg
             */
             
        protected override bool InstallBootObjects(Object extraData,string installDir)
        {
            string EFI_DIR = config.UEFI_PARTITION_MOUNTPOINT + config.UEFI_BOOT;
            string BOOT_DIR = config.UEFI_PARTITION_MOUNTPOINT + config.BOOT_GRUB;

            Log.write("===Installing Boot Objects===");

            if (!MountFirmwarePartition())
                return false;

            if (!CreateBootDirectory(EFI_DIR))
                return false;

            if (!CopyBootFiles((String)extraData,installDir,EFI_DIR))
                return false;

            if (!CopyBootGrubFiles((String)extraData,installDir, BOOT_DIR))
                return false;

            if (!CreateUEFIBootOption(config.UEFI_PARTITION_MOUNTPOINT))
                return false;          

            if (!UnMountFirmwarePartition())
                return false;

            return true;
        }

        protected override bool UnInstallBootObjects(Object extraData)
        {

            Log.write("===Removing Boot Objects===");
            MountFirmwarePartition();
            if (UEFIWrapper.UEFI_Init())
            {
                Log.write("-Remove Android UEFI Entry");
                int ret = UEFIWrapper.UEFI_DeleteBootOptionByDescription(config.BOOT_ENTRY_TEXT);
                Log.write("-UEFI: " + ret);
            }
            else
            {
                Log.write("-UEFI Init ... fail");
            }
            base.cleanup(config.UEFI_PARTITION_MOUNTPOINT + config.UEFI_BOOT);
            base.cleanup(config.UEFI_PARTITION_MOUNTPOINT + @"boot\");

            UnMountFirmwarePartition();

            return true;
        }

        private bool MountFirmwarePartition()
        {
            Log.updateStatus("Mounting EFI Partition...");
            Log.write("-Mounting EFI Partition...");
            
            string MOUNT_EXE = @"C:\Windows\System32\mountvol.exe";
            string MOUNT_CMD = string.Format(" Z: /S");

            
            if (!ExecuteCLICommand(MOUNT_EXE, MOUNT_CMD))
            {
                return false;
            }

            return true;
        }

        private bool UnMountFirmwarePartition()
        {
            Log.updateStatus("UnMounting EFI Partition...");
            Log.write("-UnMounting EFI Partition...");
            string UNMOUNT_EXE = @"C:\Windows\System32\mountvol.exe";
            string UNMOUNT_CMD = string.Format(" Z: /D");

            if (!ExecuteCLICommand(UNMOUNT_EXE,UNMOUNT_CMD))
            {
                return false;
            }

            return true;
        }

        private bool CreateBootDirectory(string directory)
        {

            Log.write("-Setup Boot Directory...");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Log.write("-Boot Folder Created: " + directory);
            }
            else
            {
                Log.write("-Boot Directory is Already Exist");
                return false;
            }

            return true;
        }

        private bool CopyBootFiles(string ISOFilePath,string InstallDirectory, string EFI_DIR)
        {
            //EFI_DIR = @"C:\AndroidOS\";

            Log.write("-Copy Boot files");
            try
            {
                //Backward Compatibility <15
                //if (Environment.Is64BitOperatingSystem)
                //    File.Copy(Environment.CurrentDirectory + @"\" + config.UEFI_GRUB_BIN64, EFI_DIR + @"\" + config.UEFI_GRUB_BIN64, false);
                //else
                //    File.Copy(Environment.CurrentDirectory + @"\" + config.UEFI_GRUB_BIN32, EFI_DIR + @"\" + config.UEFI_GRUB_BIN32, false);

                //File.Copy(Environment.CurrentDirectory + @"\" + config.UEFI_GRUB_CONFIG, EFI_DIR + @"\" + config.UEFI_GRUB_CONFIG, false);    //Android-x86
               //End
                 
                //string ExecutablePath = Environment.CurrentDirectory + @"\7z.exe";

                //testing without this \"boot\\grub\\grub.cfg\"
                //string ExecutableArgs = string.Format(" e \"{0}\" \"efi\\boot\\*\" -o{1}", ISOFilePath, EFI_DIR);   

                //Log.updateStatus("Status: Copying boot files...");
                //if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                //    return false;

                string tempDir = InstallDirectory + @"\temp\\efi\\boot\";

                if (Directory.Exists(tempDir))
                {
                    Log.updateStatus("Status: Copying boot files...");

                    foreach (string dirPath in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(tempDir, EFI_DIR));
                    }

                    foreach (string newPath in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                    {
                        File.Copy(newPath, newPath.Replace(tempDir, EFI_DIR), true);
                    }

                }

                return true;
            }
            catch (Exception ex)
            {
                Log.write(ex.Message);
                return false;
            }
            
        }

        private bool CopyBootGrubFiles(string ISOFilePath, string InstallDirectory, string BOOT_DIR)
        {
            //BOOT_DIR = @"C:\AndroidOS\";

            Log.write(" - Copy Boot files");
            try
            {
                //string ExecutablePath = Environment.CurrentDirectory + @"\7z.exe";

                ////testing with \"boot\\grub\\grub.cfg\"
                //string ExecutableArgs = string.Format(" x \"{0}\" \"boot\\grub\\grub.cfg\" \"boot\\grub\\theme\\*\" \"boot\\grub\\fonts\\*\" -o{1}", ISOFilePath, BOOT_DIR);

                //Log.updateStatus("Status: Copying boot files...");
                //if (!ExecuteCLICommand(ExecutablePath, ExecutableArgs))
                //    return false;

                string tempDir = InstallDirectory + @"\temp\\boot\";

                if (Directory.Exists(tempDir))
                {
                    Log.updateStatus("Status: Copying boot files...");

                    foreach (string dirPath in Directory.GetDirectories(tempDir, "*", SearchOption.AllDirectories))
                    {
                        Directory.CreateDirectory(dirPath.Replace(tempDir, BOOT_DIR));
                    }

                    foreach (string newPath in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                    {
                        File.Copy(newPath, newPath.Replace(tempDir, BOOT_DIR), true);
                    }

                    Directory.Delete(InstallDirectory+ @"\temp",true);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.write(ex.Message);
                return false;
            }

        }

        private bool CreateUEFIBootOption(string Drive)
        {
            string _Drive = string.Format(@"\\.\{0}",Drive);

            Log.write("-Add UEFI Entry");
            
            if (!UEFIWrapper.UEFI_Init())
            {
                Log.write("UEFI Init Fail");
                return false;
            }

            if (Environment.Is64BitOperatingSystem)
            {

                if (!UEFIWrapper.UEFI_MakeMediaBootOption(config.BOOT_ENTRY_TEXT, _Drive, config.UEFI_BOOT + config.UEFI_GRUB_BIN64))
                {
                    Log.write("UEFI 64-bit Entry Fail");
                    return false;
                }
            }
            else
            {
                if (!UEFIWrapper.UEFI_MakeMediaBootOption(config.BOOT_ENTRY_TEXT, _Drive, config.UEFI_BOOT + config.UEFI_GRUB_BIN32))
                {
                    Log.write("UEFI 32-bit Entry Fail");
                    return false;
                }
            }
            return true;
        }
    }
}
