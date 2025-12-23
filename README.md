# SSoTme CLI OST

### [View Latest Release](https://github.com/ssotme/cli/releases/latest)

[SSoTme Platform](https://explore.ssot.me/app/#!/publicTranspilers)
The Single Source of Truth Toolbox!

## About A Single Source of Truth

The **SSoTme CLI** is similar to command line package managers like NPM, Bower, Nuget, etc - however, the packages
delivered are dynamic in nature. If 100 projects install a bower package, they all get the same bytes.

SSoTme packages differ from normal packages (NPM/Bower/etc packages) in that they must be supplied with a
Single Source of Truth, a set of foundational rules, which describes the project's core functionality.
Based on your project's SSoT, you can install any SSoTme package - each one representing a specific language or
framework - and it will output your SSoT, implemented in that environment.

So, by contrast to your usual package, if 100 projects each install the same SSoTme package, they will all get
different bytes - because they will each provide a different single source of truth
which describes their project. Since each project will start with a different SSoT -
they will each get an implementation which works with their project. It will be the same KIND of
content that each package provides, but will differ in ways specific to each project.

Similarly - if one project installs 100 different SSoTme packages, their functionality will all match each other,
because they are all derived from the same SSoT - the same foundational rules.  And any time that SSoT changes,
all 100 packages will also update themselves to match the new "truth".

Overall, software built on SSoT avoids duplicating important decisions across the codebase.
Instead of scattering business rules or structural definitions throughout the source code, SSoT development
places them in one authoritative location - a central definition that drives behavior across the system.

## Open Source Tools
These tools are open source.  Eventually, the SSoT.me Website, Coordinator as well as the Codee42
and Odxml42 toolsets will also be offered as open source tools as well.  It's just a matter of
getting them cleaned up a little bit first.

## SSoT.me Architecture
SSoT.me is really a directory of Dynamic Packages (Transpilers).  The distinction between
static package managers like NPM and Bower is that each SSoT.me tool always requires INPUT.
That input is then turned into something else.  By Connecting these tools together end-to-end
a "Transpiler Pipeline" can be created which, in a very dynamic, responsive and flexible way
turns A into B.  The transaction always follows this basic script though:

1. The CLI gathers together the requeseted "input" (files, parameters, options, etc)
2. The CLI packages everything into a "Zipped Json" *Transpile Request*
3. The *Transpile Request* is sent to the SSoT.me coordinator
4. The SSoT.me Coordinator determins which tool is being requested
5. (down the road - The Coordinator Validates subscription and/or charges consumer)
6. The Coordinator forwards the request to a *Transpiler Host* for the requested tool
7. The Transpiler host processes the *Transpile Request*
8. The Transpiler sends the Output directly back to the requesting CLI (the Coordinator is not 
        involved in the response).


## Installation

You can install the SSoTme CLI by downloading the appropriate installer from the [release page](https://github.com/ssotme/cli/releases/latest).

Installing the **SSoTme Command Line Interface** tool will download the compatible .NET SDK version, and automatically
update the system path to include the CLI, allowing you to use it through the `ssotme/aicapture/aic` commands.

### Authentication

Use `ssotme -auth` or `-authenticate` to provide the CLI access to your ssot.me account.

If for some reason the authenticate command doesn't work, you can edit the configuration manually:

When you register for an account with [SSoT.me](https://aicapture.io) - you will be emailed a secret key file
that should be put in this location:

*Key File:* `%USERPROFILE%/.ssotme/ssotme.key`
```
{
   "EmailAddress": "you@domain.com",
   "Secret": "your-secret-key-here-123abc"
}
```

If you have multiple accounts, the key file should have this format: `ssotme.{account-name}.key`

For example: `ssotme.codee42.key`

### External Auth

SSoTme must communicate with external APIs, for example Airtable, to execute some commands. To set up your CLI
with the right personal access tokens for these situations, you can run `ssotme -api provider=private_key` or
`ssotme -setAccountApiKey provider/private_key`.

## Pip Install

You can also install this tool using pip:

`pip install -U git+https://github.com/ssotme/cli`

**Note:** a pip +git installation will clone the repo and run a dotnet build on the product, so you'll need both `dotnet>=8.0` and `python>=3.7`

After installation, the commands `ssotme`, `aicapture`, `aic`, & `effortless` will be usable in your terminal, and you can continue
following the setup listed above in the **Auth** section.

### Troubleshooting the PIP install

- To install the ssotme CLI via PIP, you must have dotnet & Python installed on your system. To make things as consistent as possible it's recommended to download & install Python directly from https://python.org instead of using an external package manager. **Make sure to check off the box that says "Add Python to your PATH"**
- For Linux and MacOS, in some versions of pip you may need to use `pip install git+https://github.com/ssotme/cli --break-system-packages`
- After the installer finishes, it may give a warning:
        ```WARNING: The scripts aic, aicapture and ssotme are installed in '<home directory>' which is not on PATH.```
  - To fix this, you can run the following command depending on your system:
  - **MacOS** `echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc && source ~/.zshrc`
  - **Linux** `echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc`
  - This behavior shouldn't occur on Windows, but if Python ever changes this in the future, you can resolve it by simply adding your Python installation to your system PATH.

## Uninstalling the CLI

### Uninstall with PIP

If you've installed through pip, you can uninstall by simply running: `pip uninstall ssotme -y`.

### MacOS Uninstaller

To uninstall a Mac .pkg installation, run the uninstall script by entering `/Applications/ssotme/uninstall` in the terminal.

### Windows Uninstaller

For windows, uninstall ssotme by re-running the Windows installer executable, and select 'Uninstall'.

## Syntax: `ssotme -help`
This command will show the following help.

```
Syntax: ssotme [account/]transpiler [Options]

Options:
   -account, -a           The account which the transpiler belongs to
   -addSetting, -as       Adds a setting to the SSoT.me Project
   -addTranspiler         Add a transpiler to for the given account
   -authenticate, -auth   Launch the SSoT.me website in order to authenticate (and/or register), and then to link that  user to your ssotme CLI.
   -betaRepo              Use the beta repository for this seed?
   -build, -b,
   -replay, -rebuild      Build any transpilers in the current folder (or children).
   -buildAll, -ba,
   -replayall,
   -rebuildAll            Builds all transpilers in the project
   -buildLocal, -bl,
   -replaylocal,
   -rebuildLocal          Builds only the root level transpilers, not the sub-directories.
   -checkResults, -cr     Checks the result of a build linking up input and output files of the transpiles.  Creates a SPXML file in the DSPXml folder of the project.
   -clean, -c             Don't output the final results - instead, clean
   -cleanAll, -ca         Don't output the final results - instead, clean
   -cloneSeed,
   -cs, -clone            Clones a specified seed
   -createDocs, -cd       Creates documentation based on a DSPXml file created with the -checkResults flag.
   -deleteTranspiler      Delete the transpiler with the given name
   -describeAll, -da       Describe all of the transpiler in the project
   -describe, -d          Describes the current SSoT.me Project (and all transpilers)
   -discuss, -ai          Discuss the project with an AI
   -execute, -exec        Executes the given command as a ProcessInfo.Start
   -help, -h              Show help about how to use the SSoT.me CLI
   -includeDisabled,
   -id                    Include disabled tools in the build
   -info                  View your SSoTme CLI global settings
   -init                  Initialize the current folder as the root of an SSoT.me project. An Optional parameter of force will create a sub-project.
   -input, -i             Input filename or comma separated list of file names
   -install               Saves the current command into the SSoT.me Project file
   -keyFile, -f           The keyfile to use.  By default it looks for ~/.ssotme/ssotme.key. (or ~/.ssotme/ssotme.{username}.key)
   -listSeeds, -lsd       Lists seeds available to be clones
   -listSettings, -ls     List of project settings
   -output, -o            Output filename
   -parameters, -p        A list of parameters
   -preserveZFS, -rz      Determines if the input should be preserved.
   -projectName, -name    Name of the project (optional parameter to the init command)
   -removeSetting, -rs    REmoves a setting from the SSoT.me Project
   -repoUrl               Override the default URL specified by the seed repository
   -runAs, -ra            Run as this user (look for this user's key file)
   -setAccountAPIKey,
   -api                   Add an account api key
   -skipBuild             Skips the build part of cloning a Seed repository
   -skipClean, -sc        Don't clean the output before cooking
   -transpilerGroup,
   -tg                    Name of a group to put a transpiler in within a specific folder
   -uninstall             Removes the current command from the SSoT.me Project file
   -waitTimeout, -w       The amount of time to wait for the command to continue
```
