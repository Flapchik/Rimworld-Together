def output(message="", linesbefore=0, linesafter=0, format="{}"):
    for i in range(0, linesbefore):
        print()
    print(format.format(message))
    for i in range(0, linesafter):
        print()


def success(message="", linesbefore=0, linesafter=0):
    output(message, linesbefore, linesafter, "\033[92m{}\033[0m")


def error(message="", linesbefore=0, linesafter=0):
    output(message, linesbefore, linesafter, "\033[91m{}\033[0m")


# Import Basic Packages
import os
import shutil
import subprocess
import time
from tkinter import filedialog

# Enable Console Colors
os.system('color')

# Import External Packages
try:
    import psutil
except Exception as e:
    error(e, 1, 1)
    exit(1)

# Mod ID
modSteamId = "3005289691"  # Replace with your actual Steam ID

# Mod Resources
source_dir = "ModData"
destination_dir = f"Build/{modSteamId}"

# Mod C# and DLLs
source_solution = "Source/Client/GameClient.csproj"
dll_output_dir = "Source/Client/bin/Debug/net472/"
dll_destination_dir = os.path.join(destination_dir, "Current/Assemblies/")
dll_names = ["GameClient.dll", "Newtonsoft.Json.dll", "JsonDiffPatchDotNet.dll"]

# RimWorld Directory
rimworld_dir_file = "rimworld_dir.txt"
rimworld_dir = ""

# RimWorld Execution Details
userA = "name=A"  # name and password are the same
userB = "name=B"
fastConnect = "fastConnect=true"  # shows button
instantConnect = "instantConnect=true"  # auto connects, ignores the above button
forceDevMode = "forceDevMode=true"  # shows dev mode button
instantVisit = "instantVisit=true"  # auto visits
silentVisit = "silentVisit=true"  # useful when autovisiting to not show the visit screen and bypass confirmations
# Debug Flags
printCompileLog = True


def handle_rim_world_path(possible_rimworld_dir):
    global rimworld_dir

    output(f"Checking {possible_rimworld_dir}")

    # Append the Mods folder to the RimWorld directory
    mod_path = os.path.join(possible_rimworld_dir, "Mods")

    if os.path.exists(mod_path):
        mod_specific_path = os.path.join(mod_path, modSteamId)

        # Clear the specific mod directory if it already exists
        if os.path.exists(mod_specific_path):
            shutil.rmtree(mod_specific_path)

        # The mod always gets copied in, might as well assume this is the right directory
        rimworld_dir = possible_rimworld_dir

        # Copy mod to mod_path
        shutil.copytree(destination_dir, mod_specific_path)

        success(f"{possible_rimworld_dir} is a valid RimWorld directory")

        return True

    return False


def build():
    output("Building DLLs")

    # Remove destination_dir for a fresh build
    if os.path.exists(destination_dir):
        shutil.rmtree(destination_dir)

    # Copy files from source_dir to destination_dir
    shutil.copytree(source_dir, destination_dir)

    # Build the C# project
    result = subprocess.run(["dotnet", "build", source_solution, "--configuration", "Debug"],
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)

    if printCompileLog:
        output(result.stdout, 1)

    assert "Build succeeded." in result.stdout, "Build did not succeed"
    assert "Build FAILED." not in result.stdout, "Build failed"

    # If the destination path doesn't exist, create it
    os.makedirs(dll_destination_dir, exist_ok=True)

    # Iterate over DLL names
    for dll_name in dll_names:
        # Create full paths for each DLL
        dll_output_path = os.path.join(dll_output_dir, dll_name)
        dll_destination_path = os.path.join(dll_destination_dir, dll_name)

        if not os.path.exists(dll_output_path):
            error(f"Failed to find DLL file {dll_name}")

            # Had issues with not having .NET 7.0, thought it might be worth mentioning to check the solution and possibly update VS to allow usage of .NET 7.0
            if dll_name == 'Newtonsoft.Json.Patched.dll':
                error(
                    "This DLL requires the solution to use .NET 7.0. You may need to update Visual Studio and install .NET 7.0");

            output()  # Just for a new line
            exit(1)

        # Copy the DLL file
        shutil.copy2(dll_output_path, dll_destination_path)

    success("Completed building DLLs", 0, 1)


def run():
    output("Finding RimWorld")

    default_rimworld_dirs = [
        "C:/Games/Rimworld",
        "C:/Games/Rimworld - Copy",
        "C:/Program Files (x86)/Steam/steamapps/common/RimWorld"
    ]

    if (os.path.isfile(rimworld_dir_file)):
        f = open(rimworld_dir_file, "r")
        saved_rimworld_dir = f.read()
        if (saved_rimworld_dir != ""):
            default_rimworld_dirs.insert(0, saved_rimworld_dir)

    # Copy mod to each path if it exists
    for default_rimworld_dir in default_rimworld_dirs:
        if handle_rim_world_path(default_rimworld_dir):
            break  # Early out, if we found a RimWorld path just use that

    if rimworld_dir == "":
        output("RimWorld not found, requesting installation directory")
        directory = filedialog.askdirectory(initialdir=None, mustexist=True, title="Select RimWorld Directory")

        # If no directory provided, end here
        if directory == "":
            error("No RimWorld directory provided", 1, 1)
            exit(2)

        handle_rim_world_path(directory)

    # If no directory found, end here
    if rimworld_dir == "":
        error("Failed to find RimWorld Installation Directory", 1, 1)
        exit(2)

    f = open(rimworld_dir_file, "w")
    f.write(rimworld_dir)
    f.close()

    # @TODO(jrseducate): Might be worth adding a check here if they want to open the server here as well

    # Check if we want to open instances of RimWorld for testing
    # output("Open RimWorld? (Y/N, Default:Y)", 1)
    # answer = input()
    # no = answer == "n" or answer == 'N'
    #
    # output()  # Extra Line
    #
    # if (no):
    #     exit(0)

    success("Now opening RimWorld")

    norm_exe_path = os.path.normcase(os.path.realpath(os.path.join(rimworld_dir, "RimWorldWin64.exe")))

    # Check if the process is already running
    for proc in psutil.process_iter(['pid', 'name', 'exe']):
        # Check whether the process name matches
        if proc.info['exe'] and os.path.normcase(os.path.realpath(proc.info['exe'])) == norm_exe_path:
            proc.kill()  # If so, kill the process

    subprocess.Popen([norm_exe_path, userA, fastConnect, instantConnect, forceDevMode, instantVisit, silentVisit])
    time.sleep(2)  # prevents the second instance from overwriting the save of the first during loading
    subprocess.Popen([norm_exe_path, userB, fastConnect, instantConnect, forceDevMode, silentVisit])


try:
    build()
    run()
except Exception as e:
    error(e, 1, 1)
    exit(1)

exit(0)
