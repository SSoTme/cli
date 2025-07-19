from setuptools import setup
import shutil
import json
import os
import platform
import subprocess
import re

from ssotme.cli import BASE_SUPPORTED_DOTNET, get_release_path, get_base_version_str, get_home_ssotme_dir


class DotNetInstallError(Exception):
    pass


class Installer:
    def __init__(self):
        self.BASE_VERSION = "2024.08.23"  # fallback if package.json is not found
        self.BASE_SUPPORTED_DOTNET = BASE_SUPPORTED_DOTNET
        self._dotnet_executable_path = None
        self.home_dir, self.ssotme_dir = get_home_ssotme_dir()

    @property
    def dotnet_executable_path(self):
        if self._dotnet_executable_path is None:
            raise Exception("DotNet executable path was not set!")
        return self._dotnet_executable_path

    @property
    def is_windows(self):
        return platform.system() == "Windows"

    @property
    def is_macos(self):
        return platform.system() == "Darwin"

    @property
    def is_linux(self):
        return platform.system() == "Linux"

    @staticmethod
    def get_cli_handler_path():
        base_dir = os.path.dirname(os.path.abspath(__file__))
        the_path = os.path.join(base_dir, "Windows", "Lib", "CLIOptions", "SSoTmeCLIHandler.cs")
        if not os.path.exists(the_path):
            raise FileNotFoundError(f"Could not find {the_path}")
        return the_path

    def get_dotnet_executable_path(self):
        """Get the absolute path to the dotnet executable."""
        dotnet_path = os.path.join(self.home_dir, ".dotnet", "dotnet")
        dotnet_windows_path = os.path.join(self.home_dir, ".dotnet", "dotnet.exe")

        # Check if the dotnet executable exists
        if os.path.isfile(dotnet_path) and os.access(dotnet_path, os.X_OK):
            print(f"Found dotnet executable at: {dotnet_path}")
            self._dotnet_executable_path = dotnet_path
        elif os.path.isfile(dotnet_windows_path) and os.access(dotnet_windows_path, os.X_OK):
            print(f"Found dotnet executable at: {dotnet_windows_path}")
            self._dotnet_executable_path = dotnet_windows_path
        else:
            print("WARN: Dotnet executable not found in ~/.dotnet directory")

    def is_dotnet_version_installed(self, required_version):
        # get all installed dotnet versions
        try:
            result = subprocess.run([self.dotnet_executable_path, "--list-sdks"],
                                    check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            sdk_versions = result.stdout.decode().splitlines()
            for line in sdk_versions:
                version = line.split(" ")[0]
                # we can just check if the base versions are the same (dont need to be too picky)
                if get_base_version_str(version) == get_base_version_str(required_version):
                    return True
            return False
        except Exception as e:
            print(f"Error checking installed dotnet version - {type(e).__name__}: {str(e)}")
            return False

    def get_package_version(self):
        print("Fetching package version from package.json")
        version = self.BASE_VERSION
        try:
            with open("package.json") as f:
                txt = f.read()
                j = json.loads(txt)
                version = j["version"]
        except FileNotFoundError:
            print("Could not find package.json")
        except json.decoder.JSONDecodeError:
            print("Could not parse package.json")
        except Exception as e:
            print(f"Error {type(e).__name__}: {str(e)}")

        print(f"Package version is '{version}'")
        return version

    def get_supported_dotnet_version(self):
        print("Fetching supported dotnet version from package.json")
        version = self.BASE_SUPPORTED_DOTNET
        try:
            with open("package.json") as f:
                txt = f.read()
                j = json.loads(txt)
                version = j["dotnet"]
        except FileNotFoundError:
            print("Could not find package.json - using default version")
        except json.decoder.JSONDecodeError:
            print("Could not parse package.json - using default version")
        except Exception as e:
            print(f"Error getting supported version {type(e).__name__}: {str(e)} - using default version")

        print(f"Specified dotnet version is '{version}'")
        return version

    def build_dotnet_project(self):
        """Build the .NET project with the Release configuration."""
        print("Building .NET project...")

        # Get the directory where setup.py is located
        base_dir = os.path.dirname(os.path.abspath(__file__))
        # Navigate to the directory containing the .sln file
        os.chdir(base_dir)

        # Build the project
        print(f"Run {self.dotnet_executable_path} build SSoTme-OST-CLI.sln -c Release")
        try:
            result = subprocess.run(
                [
                    self.dotnet_executable_path,
                    "build", "SSoTme-OST-CLI.sln",
                    "--configuration", "Release",
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True
            )

            if result.returncode != 0:
                print(f"Error building .NET project:\n{result.stderr}\n{result.stdout}")
                return False

            print("Build completed successfully.")
            return True
        except FileNotFoundError:
            print("Warning: .sln file not found, skipping build")
            return True
        except Exception as e:
            print(f"Error during build: {e}")
            return False

    def _install_dotnet_sh_script(self, home_dir):
        base_dir = os.path.dirname(os.path.abspath(__file__))
        if self.is_macos:
            try:
                # is wget available?
                if not shutil.which("wget"):
                    print("Installing wget via brew...")
                    subprocess.run(["brew", "install", "wget"], check=True)
            except Exception as e:
                # fallback to curl
                if shutil.which("curl"):
                    print(f"Brew failed ({e}), but curl is available")
                else:
                    raise DotNetInstallError(f"Neither wget nor curl is available and brew failed: {e}")
        elif self.is_linux:
            wget_path = shutil.which("wget")
            if not wget_path:
                cmd = f"apt-get update && apt-get install -y wget"
                try:
                    # try without sudo
                    print(f"Attempting to install wget: {cmd}")
                    subprocess.run(cmd, shell=True, check=True)
                except Exception:
                    cmd = f"sudo apt-get update && sudo apt-get install -y wget"
                    print(f"Attempting with sudo: {cmd}")
                    try:
                        subprocess.run(cmd, shell=True, check=True)
                    except Exception as e:
                        raise DotNetInstallError(f"Failed to install wget: {e}\n\nPlease install wget manually and retry")
        else:
            if self.is_windows:
                print("Unneeded call to install_dotnet_sh_script (windows does not need it)")
                raise DotNetInstallError("install_dotnet_sh_script is not supported on Windows")
            else:
                print(f"Unsupported platform: {platform.system()}")
                raise DotNetInstallError(f"Unsupported platform: {platform.system()}")

        # download the .sh script provided my microsoft that downloads dotnet
        script_path = os.path.join(home_dir, "dotnet-install.sh")
        if shutil.which("curl"):
            try:
                print("Downloading dotnet-install.sh with curl...")
                subprocess.run(["curl", "-sSL", "https://dot.net/v1/dotnet-install.sh", "-o", script_path], check=True)
                return script_path
            except Exception as e:
                try:
                    print(f"Curl failed ({e}), trying with sudo...")
                    subprocess.run(["sudo", "curl", "-sSL", "https://dot.net/v1/dotnet-install.sh", "-o", script_path], check=True)
                    return script_path
                except Exception as sudo_e:
                    raise DotNetInstallError(f"Failed to download install script with curl: {sudo_e}")
        elif shutil.which("wget"):
            try:
                print("Downloading dotnet-install.sh with wget...")
                subprocess.run(["wget", "https://dot.net/v1/dotnet-install.sh", "-P", base_dir], check=True)
                return script_path
            except Exception as e:
                try:
                    print(f"Wget failed ({e}), trying with sudo...")
                    subprocess.run(["sudo", "wget", "https://dot.net/v1/dotnet-install.sh", "-P", base_dir], check=True)
                    return script_path
                except Exception as sudo_e:
                    raise DotNetInstallError(f"Failed to download install script with wget: {sudo_e}")
        else:
            raise DotNetInstallError("Neither curl nor wget is available. Please install one and retry.")

    def install_dotnet(self, version: str):
        """Attempt to install .NET SDK of a specific version."""
        try:
            print("Installing DotNet...")
            if self.is_macos or self.is_linux:
                script_path = self._install_dotnet_sh_script(self.ssotme_dir)
                try:
                    os.chmod(script_path, 0o755)  # make the sh script executable
                except Exception:
                    # try again with sudo
                    try:
                        subprocess.run(["sudo", "chmod", "+x", script_path], check=True)
                    except Exception as e:
                        raise DotNetInstallError(f"Failed to make install script executable: {e}")
                
                # run the install script
                try:
                    print(f"Running dotnet-install.sh for version {version}...")
                    subprocess.run([script_path, "--version", version], check=True)
                except Exception as e:
                    try:
                        print(f"Install without sudo failed ({e}), trying with sudo...")
                        subprocess.run(["sudo", script_path, "--version", version], check=True)
                    except Exception as sudo_e:
                        raise DotNetInstallError(f"Failed to install .NET SDK: {sudo_e}")
                        
            elif self.is_windows:
                try:
                    install_script = os.path.join(self.ssotme_dir, "dotnet-install.ps1")
                    dotnet_install_dir = os.path.join(self.home_dir, ".dotnet")

                    # download install script - ensure directory exists
                    os.makedirs(os.path.dirname(install_script), exist_ok=True)
                    cmd = f"Invoke-WebRequest https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.ps1 -OutFile {install_script}"
                    print(cmd)
                    subprocess.run(["powershell", "-Command", cmd], check=True)

                    # create installation directory
                    os.makedirs(dotnet_install_dir, exist_ok=True)
                    print(f"Installing .NET SDK version {version} to {dotnet_install_dir}...")
                    subprocess.run([
                        "powershell",
                        "-ExecutionPolicy", "Bypass",
                        "-File", install_script,
                        "-Version", version,
                        "-InstallDir", dotnet_install_dir
                    ], check=True)
                except Exception as e:
                    raise DotNetInstallError(f"Failed to install .NET SDK: {e}")
            else:
                raise DotNetInstallError(f"Unsupported platform: {platform.system()}")

        except DotNetInstallError as e:
            print(f"ERROR: {str(e)}")
            print("\n===== DOTNET INSTALLATION INSTRUCTIONS =====")
            print(f"1. Download .NET SDK v{version} from: https://dotnet.microsoft.com/download/dotnet/{get_base_version_str(version)}")
            print("2. Follow the installation instructions for your platform")
            print(f"3. Re-run this installation after .NET is installed")
            print("=============================================\n")
            raise RuntimeError(f"DotNet installation failed: {e}")
        except Exception as e:
            print(f"Unexpected error during .NET installation: {e}")
            print("Please install dotnet SDK manually from https://dotnet.microsoft.com/download and re-run this pip installation")
            raise RuntimeError(f"DotNet installation failed: {e}")

    def get_installed_sdk_versions(self):
        """Get all installed .NET SDK versions."""
        try:
            result = subprocess.run([self.dotnet_executable_path, "--list-sdks"], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            sdk_versions = []
            for line in result.stdout.decode().splitlines():
                if line.strip():
                    version = line.split(" ")[0]
                    sdk_versions.append(version)
            return sdk_versions
        except Exception as e:
            print(f"Error checking installed dotnet SDKs - {type(e).__name__}: {str(e)}")
            return []

    def save_dotnet_info(self, version):
        # create a custom dotnet configuration in the user's home directory
        ssotme_version = self.get_package_version()
        sdk_versions = self.get_installed_sdk_versions()
        exe_path = self._dotnet_executable_path
        exe_path = exe_path if exe_path is not None else "dotnet"
        info = {
            "installed_versions": sdk_versions,
            "using_version": version,
            "executable_path": exe_path,
            "ssotme_version": ssotme_version,
        }
        with open(os.path.join(self.ssotme_dir, "dotnet_info.json"), "w") as f:
            json.dump(info, f, indent=2)
        print(f"Saved .NET SDK information to dotnet_info.json")

    def run_installer(self):
        """Run the installation process before setup package"""
        try:
            supported_version = self.get_supported_dotnet_version()

            # check if we need to install dotnet
            self.get_dotnet_executable_path()
            if self._dotnet_executable_path is None or not self.is_dotnet_version_installed(supported_version):
                # dotnet isn't installed, or the wrong version is installed
                print(f"DotNet v{supported_version} not installed - installing it now")
                self.install_dotnet(supported_version)
                
                # verify dotnet installed
                self.get_dotnet_executable_path()
                if not self._dotnet_executable_path:
                    raise RuntimeError("DotNet executable path could not be found after installation")
                    
                if self.is_dotnet_version_installed(supported_version):
                    print(f"DotNet v{supported_version} successfully installed")
                else:
                    raise RuntimeError(
                        f"The dotnet version specified in the package.json ({supported_version}) was not detected "
                        f"after installation. Please manually install .NET SDK v{supported_version} from: "
                        f"https://dotnet.microsoft.com/en-us/download/dotnet/{get_base_version_str(supported_version)}"
                    )
            else:
                print(f"Found existing dotnet v{supported_version} installation")

            # save dotnet info json
            self.save_dotnet_info(supported_version)

            # change cli version in cs file so `ssotme -version` works
            print("Updating CLI version in the CLI handler...")
            try:
                fp = self.get_cli_handler_path()
                with open(fp, "r") as f:
                    contents = f.read()

                pattern = r'public string CLI_VERSION = ".*?";'
                replacement = f'public string CLI_VERSION = "{self.get_package_version()}";'

                new_contents = re.sub(pattern, replacement, contents)

                with open(fp, "w") as f:
                    f.write(new_contents)

                print("CLI version updated successfully.")
            except Exception:
                print("WARNING: Failed to get CLI handler path to update version - CLI may print incorrect version")

            # build dotnet project
            build_result = self.build_dotnet_project()
            if not build_result:
                raise RuntimeError("Failed to build .NET project. Please check the build logs for errors.")

            # the built source is now in cli/Windows/CLI/bin/Release
            try:
                built_proj = get_release_path(supported_version, base_dir=os.path.dirname(os.path.abspath(__file__)))
            except FileNotFoundError as e:
                raise RuntimeError(f"Could not find build output: {e}")

            # we need to copy it into cli/ssotme/lib/Windows/Release so that it's part of the
            # ssotme pip package and is copied to site-packages later
            if not os.path.exists(built_proj):
                raise RuntimeError(
                    f"Could not find build output at {built_proj}. "
                    "This may indicate a problem with the .NET build process."
                )

            # create the cli/ssotme/lib/Windows/CLI/bin/Release/net7.0 dir structure
            base_dir = os.path.dirname(os.path.abspath(__file__))
            cli_dir = os.path.join(base_dir, "ssotme")
            os.makedirs(cli_dir, exist_ok=True)
            windows_dir = os.path.join(cli_dir, "lib", "Windows", "CLI", "bin", "Release", f"net{get_base_version_str(supported_version)}")
            try:
                os.makedirs(windows_dir, exist_ok=True)
            except Exception as e:
                raise RuntimeError(f"Failed to create directory for build output: {e}")

            # copy
            try:
                shutil.copytree(built_proj, windows_dir, dirs_exist_ok=True)
                print(f"Copied build output from {built_proj} to {windows_dir}")
            except Exception as e:
                raise RuntimeError(f"Failed to copy build output: {e}")
                
            print("Installation completed successfully!")
            print("You can now use the 'ssotme', 'aicapture', or 'aic' commands from your terminal.")
            
        except Exception as e:
            print("\n" + "=" * 60)
            print("INSTALLATION ERROR")
            print("=" * 60)
            print(f"Error: {str(e)}")
            print("\nTroubleshooting steps:")
            print("1. Ensure you have the correct permissions to install packages")
            print("2. Try running the installation with administrative privileges")
            print("3. Check that your system meets the requirements (.NET SDK compatibility)")
            print("4. If the problem persists, please report the issue with the error details above")
            print("=" * 60 + "\n")
            raise


def run_setup():
    try:
        # run installation -> will install dotnet & build the project
        installer = Installer()
        installer.run_installer()
        
        setup_config = {
            "name": "ssotme",
            "version": installer.get_package_version(),
            "description": "Python wrapper for installing SSoTme CLI tools",
            "author": "SSoTme",
            "author_email": "michael@ssot.me",
            "url": "https://ssot.me",
            "license": "GNU",
            "include_package_data": True,
            "packages": ["ssotme"],
            "package_data": {
                "ssotme/lib": ["lib/**/*"]
            },
            "entry_points": {
                "console_scripts": [
                    "ssotme = ssotme.cli:main",
                    "aicapture = ssotme.cli:main",
                    "aic = ssotme.cli:main",
                ]
            },
            "classifiers": [
                "Development Status :: 4 - Beta",
                "Intended Audience :: Developers",
                "License :: OSI Approved :: GNU General Public License (GPL)",
                "Programming Language :: Python :: 3.7",
                "Programming Language :: Python :: 3.8",
                "Programming Language :: Python :: 3.9",
                "Programming Language :: Python :: 3.10",
                "Programming Language :: Python :: 3.11",
            ],
            "python_requires": '>=3.7',
            "install_requires": [],
        }
        setup(**setup_config)
        
    except Exception as e:
        print("\n" + "=" * 60)
        print("SETUP ERROR")
        print("=" * 60)
        print(f"Error: {str(e)}")
        print("\nThe installation process failed. Please check the error message above.")
        print("If you need assistance, please open an issue at https://github.com/SSoTme/cli/issues/new with the error details.")
        print("=" * 60 + "\n")
        # re-raise the error into pip
        raise


if __name__ == "__main__":
    run_setup()
else:
    # when imported (not ran) we don't want to run the installer
    print("Note: setup.py imported as a module, installation process will run when setup() is called.")

# if it throws "dotnet is already installed -> dotnet wasn't installed successfully" then do rm -rf ~/.dotnet and try again
