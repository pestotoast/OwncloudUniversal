$vsixPath = "$($env:USERPROFILE)\sqlite-uwp-3180000.vsix"
(New-Object Net.WebClient).DownloadFile('https://www.sqlite.org/2017/sqlite-uwp-3180000.vsix', $vsixPath)
"`"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\VSIXInstaller.exe`" /q /a $vsixPath" | out-file ".\install-vsix.cmd" -Encoding ASCII
& .appveyor\install-vsix.cmd