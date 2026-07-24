set SEInstallDir="R:\SteamLibrary\steamapps\common\SpaceEngineers"
for %%I in (.) do set ParentDirName=%%~nxI
%SEInstallDir%\Bin64\SEWorkshopTool.exe push --mods "%ParentDirName%" --exclude-ext .bat .psd .fbx .hkt .xml .blend .blend1 .gitignore .gitattributes .log .git .mod
pause