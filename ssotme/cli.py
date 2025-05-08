import sys
import subprocess
import shutil
import json
import os


BASE_SUPPORTED_DOTNET = "7.0.410"


def get_home_ssotme_dir():
    home_dir = os.path.expanduser("~")
    ssotme_dir = os.path.join(home_dir, ".ssotme")
    if not os.path.exists(ssotme_dir):
        os.makedirs(ssotme_dir)

    return home_dir, ssotme_dir


def get_base_version_str(fullstr: str):
    dotnet_base_version = fullstr.split('.')
    return dotnet_base_version[0] + '.' + dotnet_base_version[1]


def get_release_path(dotnet_version: str, base_dir):
    """Get the path to the built project (Windows/CLI/bin/Release/...)"""
    # trim off the final version number (v.x.x -> v.x)
    the_path = os.path.join(base_dir, "Windows", "CLI", "bin", "Release", f"net{get_base_version_str(dotnet_version)}")
    if not os.path.exists(the_path):
        raise FileNotFoundError(f"Could not find {the_path}")
    return the_path


def get_dll_path(dotnet_version: str) -> str:
    """Get the dll path relative to this file (ssotme/cli.py -> ssotme/lib/Windows/CLI/bin/Release/...)"""
    base_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "lib")
    the_path = os.path.join(get_release_path(dotnet_version, base_dir), "SSoTme.OST.CLI.dll")
    if not os.path.exists(the_path):
        raise FileNotFoundError(f"Could not find {the_path}")
    return the_path


def get_dotnet_info() -> (str, str):
    """Get the saved .NET SDK information."""
    home, ssotme = get_home_ssotme_dir()
    dotnet_info_path = os.path.join(ssotme, "dotnet_info.json")
    try:
        with open(dotnet_info_path, "r") as f:
            return dotnet_info_path, json.load(f)
    except Exception:
        print(f"FATAL: Could not find dotnet_info.json\n"
              f"If it's been moved or modified, please make sure it's in the correct path\n"
              f"Or if it was deleted, re-install ssotme from https://github.com/ssotme/cli or using pip\n\n"
              f"Error reading dotnet_info.json at {dotnet_info_path}\n")
        raise


def main():
    info_filepath, dotnet_info = get_dotnet_info()
    if dotnet_info is not None and "executable_path" in dotnet_info and os.path.exists(dotnet_info["executable_path"]):
        dotnet = dotnet_info["executable_path"]
    else:
        # fall back to using 'dotnet' command (not direct path to exe)
        dotnet = shutil.which("dotnet")
        if not dotnet:
            print("dotnet is not installed or not in PATH.")
            sys.exit(1)

    # Get version from saved info or package.json
    version = BASE_SUPPORTED_DOTNET
    if dotnet_info and "using_version" in dotnet_info:
        version = dotnet_info["using_version"]
    
    dll_path = get_dll_path(version)

    args = sys.argv[1:]
    code = 0
    if len(args) > 0 and (args[0] == "--info" or args[0] == "-i"):
        # print debugging info
        print(f"SSOTME Version: {dotnet_info['ssotme_version']}\n"
              f"Configured to use .NET SDK {version}\n"
              f"Configured to use .NET executable: {dotnet}\n"
              f"Using config file: {info_filepath}\n")

        # verify the dotnet version being used
        result = subprocess.run([dotnet, "--version"], stdout=subprocess.PIPE)
        dotnet_version = result.stdout.decode().strip()
        if dotnet_version != version:
            print(f"WARNING: .NET SDK version does not match .NET executable - configured to use .NET SDK {version}, but `{dotnet} --version` returned {dotnet_version}\n")

    else:
        # run the actual cli
        try:
            result = subprocess.run([dotnet, dll_path] + args)
            code = result.returncode
        except Exception as e:
            print(f"Execution failed: {e}")
            code = 1

    sys.exit(code)

# todo: we're tracking which dotnet sdk version we should use in package.json['dotnet'], and across the python codebase
# The package.json['dotnet'] also tells setup.py which dotnet version to automatically install.
# the question is whether we need to keep tracking the version (do we need to warn users?)
# if so, we could just add these to path to force dotnet to use a specific version:

# export DOTNET_ROOT=$HOME/dotnet
# export PATH=$DOTNET_ROOT:$PATH

# set DOTNET_ROOT=C:\path\to\sdk
# set PATH=%DOTNET_ROOT%;%PATH%
