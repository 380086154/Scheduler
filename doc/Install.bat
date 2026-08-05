C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe SessionService.exe

Net Start "Session Service"

sc config "Session Service" start= auto
