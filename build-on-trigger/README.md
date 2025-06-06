# Connecting the SSoTme CLI to your Copilot Instance

Copilot can help you edit your Single Source of Truth! All you need is a Baserow Account with a database with at least one table in it.

To get started, first run this ssotme command to connect the cli to your baserow account:

`ssotme -api baserow="username/password"`

Now, copy the baseId of your Baserow database with the tables you want to edit. You'll need this for running the next command.

# Install the SSoTme Agent Copilot Plugin

... coming soon

# Run

Now you're set! Run the below command to connect to copilot through our bridge server:

`ssotme -build -buildontrigger=baseId -copilotConnect`


Now the SSoTme Copilot Agent can read and edit your Baserow database!

Editing your Baserow database via this tool will automatically re-build your code - and a Baserow-compatible ssotme transpiler is in the works as well.
