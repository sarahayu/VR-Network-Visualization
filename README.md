# VR Network Visualization

![Main view of application](images/mainview.png)

## Development

### Equipment

These are the tools used at our time of development, and by no means are hard requirements (i.e. you can use MacOS instead of Windows, but setup may differ).
- Unity 2022.3.62f2
- Oculus Rift/Quest
- Windows 11
- Windows Subsystem Linux 2
- python3.10

### Install

1. Install Neo4J.
    - Go to their [download page](https://neo4j.com/deployment-center/#gdb-tab)
    - Scroll down to the section `Neo4j Desktop`
    - Select `Windows`
    - Click `Download`
    ![Screenshot of page where to download Neo4J from](images/downloadscreen.png)

2. Set up language server.
    - Go to folder `Assets/Voice/PythonScripts`
    - Run `install.sh`


### Running

1. Run Neo4J server.

2. Run language server.
    - Go to folder `Assets/Voice/PythonScripts`
    - Run `run.sh`

3. Click `Play` in Unity.