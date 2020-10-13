# SafeExecute
Patches ETW EventWrite function to return immediately to evade detection via ETW while using Execute-Assembly.

The solution comes with two projects:
1. PreparePayload
2. SafeExecute

Where PreparePayload is used to embed the target executable to the SafeExecute. And SafeExecute that does the actual pathcing and runnig the target .NET executable.

## PreparePayload
Tool is used to gzip compress the target binary to be used with the SafeExecute. The favicon.ico file from SafeExecute project should be subsituted with the PreparePayload output.

The idea and code has been copied from NoAmci project: https://github.com/med0x2e/NoAmci
### Usage
Build the project and run the executable. First argument is the target executable and second is the output path.
```
.\PreparePayload.exe "C:\Users\admin\Desktop\SharpHound.exe" "C:\Users\admin\Source\Repos\SafeExecute\SafeExecute\favicon.ico"
```
### Output
```
[+] Reading target binary: C:\Users\admin\Desktop\SharpHound.exe
[+] Wrote the manipulated assembly file to: C:\Users\admin\Source\Repos\SafeExecute\SafeExecute\favicon.ico
```

## SafeExecute
SafeExecute patches the out the EtwEventWrite function in order to hide the loaded assembly. The application uses the P/Invoke technique for the patching, so keep that in mind when thinking about opsec.
The target application is the embedded resource "favicon.ico" that is decompressed and deflated back to the original executable and then loaded and executed via reflection.

Thanks for XPN for doing all the actual work:
https://blog.xpnsec.com/hiding-your-dotnet-etw/

SafeExecute supports passing arguments to the target application, so there should be no need to recompile it every time for every command.
Arguments are: Namespace.Class, Function and additional parameters.
### Usage
Example of running SeatBelt
```
.\SafeExecute_SeatBelt.exe Seatbelt.Program Main System
```
## Output
```
[+] Detecting os arch
[+] Selected x64
[+] Attempting to patch EtwEventWrite with instruction c30000
[+] Original value:008B4C
[+] New value:0000C3


                        %&&@@@&&
                        &&&&&&&%%%,                       #&&@@@@@@%%%%%%###############%
                        &%&   %&%%                        &////(((&%%%%%#%################//((((###%%%%%%%%%%%%%%%
%%%%%%%%%%%######%%%#%%####%  &%%**#                      @////(((&%%%%%%######################(((((((((((((((((((
#%#%%%%%%%#######%#%%#######  %&%,,,,,,,,,,,,,,,,         @////(((&%%%%%#%#####################(((((((((((((((((((
#%#%%%%%%#####%%#%#%%#######  %%%,,,,,,  ,,.   ,,         @////(((&%%%%%%%######################(#(((#(#((((((((((
#####%%%####################  &%%......  ...   ..         @////(((&%%%%%%%###############%######((#(#(####((((((((
#######%##########%#########  %%%......  ...   ..         @////(((&%%%%%#########################(#(#######((#####
###%##%%####################  &%%...............          @////(((&%%%%%%%%##############%#######(#########((#####
#####%######################  %%%..                       @////(((&%%%%%%%################
                        &%&   %%%%%      Seatbelt         %////(((&%%%%%%%%#############*
                        &%%&&&%%%%%        v0.2.0         ,(((&%%%%%%%%%%%%%%%%%,
                         #%%%%##,



=== Running System Triage Checks ===
```
