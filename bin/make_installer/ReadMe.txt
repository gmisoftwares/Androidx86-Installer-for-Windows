# This is a more simple way to create the .exe installer using a 7-zip script instead. 
# First step is to go to the Androidx86-Install* folder and use 7-zip to grab it all into a .7z file.
# Then move that .7z file to this folder. 
# 
# Next we edit the folder name for that version, and then edit the make_installer.bat 
# file to reflect those changes. Example below:
# 	copy /b 7zS.sfx + config.txt + Androidx86-Installv28.5800.7z Androidx86-Installv28.5800.exe


//run 7z.exe a Androidx86-Installv28.5800.7z