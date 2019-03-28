# USB-Blocker
Application written in C# for blocking usb devices with devcon (Microsoft Device Console) on Windows 10 , 8.1 , 8 , 7 and XP.
Right now it should work relativelly good on x86 and x64 versions of Windows 10 , 8.1 , 8 , 7 .
On windows XP it doesn't trigger device disable when enabling device posibly because pnp device instance creation isn't supported which means I need to implement device disabling not based on Windows Runtime events.
This is a work in progress so have some patience :P .
App requires admin rights if installed in Program Files.
If un installed it in Windows unprotected folders it doesn't require admin rights.
App reguires .Net Framework 4 to be installed in system. Hopefully in the future ill rewrite project so it doesn't require this.
After installation it requires a system restart for the service to start.
