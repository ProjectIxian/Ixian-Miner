# Ixian-Miner
Mining software for the Ixian cryptocurrency

## Development branches

There are two main development branches:
* master: This branch is used to build the binaries for the official IXIAN DLT network. It should change slowly and be quite well-tested. This is also the default branch for anyone who wishes to build their Ixian software from source.
* development: This is the main development branch and the source for testnet binaries. The branch might not always be kept bug-free, if an extensive new feature is being worked on. If you are simply looking to build a current testnet binary yourself, please use one of the release tags which will be associated with the master branch.


## Running
Download the latest binary release or you can compile the code yourself.
### Windows
Right-click the run.bat file in the IxianMiner directory and choose Edit. In the notepad window, modify YOUR_ADDRESS with your Ixian wallet address and WORKER_NAME with a name for your mining rig. Then choose File -> Save and close the window. Double-click on the run.bat file to start mining to your wallet address.

or

Open a terminal in the IxianMiner directory and type
```
IxianMiner.exe -h
```
to find out how to configure and run the IxianMiner.

### Linux
Download and install the latest Mono release for your Linux distribution. 
The default Mono versions shipped with most common distributions are outdated.

Go to the [Mono official website](https://www.mono-project.com/download/stable/#download-lin) and follow the steps for your Linux distribution.
We recommend you install the **mono-complete** package.

Open a terminal and navigate to the IxianMiner folder, then type
```
IxianMiner.exe -h
```
to find out how to configure and run the IxianMiner.

## Get in touch / Contributing

If you feel like you can contribute to the project, or have questions or comments, you can get in touch with the team through Discord: (https://discord.gg/dbg9WtR)

## Pull requests

If you would like to send an improvement or bugfix to this repository, but without permanently joining the team, follow these approximate steps:

1. Fork this repository
2. Create a branch (preferably with a name that describes the change)
3. Create commits (the commit messages should contain some information on what and why was changed)
4. Create a pull request to this repository for review and inclusion.
