# AI Capture CLI OST
[AICapture Platform](http://aicapture.io)
The Single Source of Truth Toolbox!

## Additional Docs
In addition to the summary below - there is [additional documentation](https://aicapture.github.io/AICapture-Open-Source-Tools/) on
the github.io site.

## About A Single Source of Truth
Software based on a single source of truth is software that does not tolorate duplication of 
Authority.  "Source Code" is frequently a terrible place for decisions about how software
should work.  SSoT development is based on the notion that there should always be one,
Authoritative place for these decisions.  

*AICapture* is a command line package manager like NPM, Bower, Nuget, etc - however, the packages
delivered are dynamic in nature.  If 100 projects install a bower package, they all get the
same bytes.

If, by contrast, 100 projects each install the same SSoT.me package, they will all get
different bytes - because they will each be required to provide a single source of truth
which describes their project.  Since each project will start with a different SSoT - 
they will each get a package which works with their project.  It will be the same KIND of 
content that the other projects all needed, but will differ in ways specific to each project.

Similarly - if you one project installs 10 AICapture packages, they will all match each other,
because they are all derived from the same SSoT.  And any time that SSoT changes, all 10
packages will also update themselves to match the new "truth".

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
Installing the *AICapture executable* command line tool will automatically update the path
to include the CLI.  

When you register for an account with [SSoT.me](https://aicapture.io) - you will be emailed a secret key
That file should be put in this location:

*Key File:* `%USERPROFILE%/.aicapture/aicapture.key`
```
{
   "emailAddress": "you@domain.com",
   "secret": "your-secret-key-here-123abc"
}
```

If you have multiple accounts, the format key file should have ethis format: `aicapture.{account-name}.key`
For example: `aicapture.codee42.key`

## Pip Install

You can also install this tool using pip: 

`pip install -U git+https://github.com/ssotme/cli`

This will also attempt to automatically install the required .NET version into your system.

After installation, the commands `ssotme`, `aicapture`, and `aic` will be usable in your terminal!

### Troubleshooting the PIP install

- To use PIP, you must have Python installed on your system. To make things as consistent as possible it's recommended to download & install Python directly from https://python.org instead of using an external package manager. **Make sure to check off the box that says "Add Python to your PATH"**
- For Linux and MacOS, in some versions of pip you may need to use `pip install git+https://github.com/ssotme/cli --break-system-packages`
- After the installer finishes, it may give a warning: 
        ```WARNING: The scripts aic, aicapture and ssotme are installed in '<home directory>' which is not on PATH.```
  - To fix this, you can run the following command depending on your system:
  - **MacOS** `echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc && source ~/.zshrc`
  - **Linux** `echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc && source ~/.bashrc`
  - This behavior shouldn't occur on Windows, but if Python ever changes this in the future, you can resolve it by simply adding your Python installation to your system PATH.

### Uninstall with PIP

If you've installed through pip, you can uninstall by simply running: `pip uninstall ssotme -y`. Note that the DotNet SDK that was installed along with `ssotme` won't be uninstalled automatically, though.

## Syntax: `aicapture -help`
This command will show the following help.

```
aicapture [account/]transpiler [parameters,...] [options]

options:
   -account, -a          The account which the transpiler belongs to
   -addSetting, -as      Adds a setting to the aicapture.json
   -build, -b            Build any transpilers in the
                         current folder (or children).
   -buildAll, -ba        Builds all transpilers in the project
   -checkResults, -cr    Checks the result of a build linking up input
                         and output files of the transpiles.  Creates a
                         SPXML file in the DSPXml folder of the project.
   -clean, -c            Don't output the final results - instead, clean
   -cleanAll, -ca        Don't output the final results - instead, clean
   -createDocs, -cd      Creates documentation based on a DSPXml
                         file created with the -checkResults flag.
   -descibeAll, -da      Descibe all of the transpiler in the project
   -describe, -d         Describes the current AICapture
                         project (and all transpilers)
   -emailAddress, -e     The email address for the account authenticating
   -execute, -exec       Executes the given command as a ProcessInfo.Start
   -help, -h             Show help about how to use the aicapture cli
   -includeDisabled,
   -id                   Include disabled tools in th ebuild
   -init                 Initialize the current folder as
                         the root of an SSOT.me project
   -input, -i            Input filename or comma separated list of file names
   -install              Saves the current command into the aicapture.json file
   -keyFile, -f          The keyfile to use.  By default
                         it looks for ~/.aicapture/aicapture.key.
   -listSettings, -ls    List of project settings
   -output, -o           Output filename
   -parameters, -p       A list of parameters
   -preserveZFS, -rz     Determines if the input should be preserved.
   -removeSetting, -rs   REmoves a setting from the aicapture project
   -runAs, -ra           Run as this user (look for this user's key file)
   -secret, -k           The secret associated with that email address
   -skipClean, -sc       Don't clean the output before cooking
   -uninstall            Removes the current command
                         from the AICapture project file
   -waitTimeout, -w      The amount of time to wait
                         for the command to continue
```

