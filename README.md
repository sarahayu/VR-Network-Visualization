# VR Network Visualization

## Development

### Equipment
These are the tools used at our time of development, and by no means are hard requirements (i.e. you can use MacOS instead of Windows, but setup may differ).
- Unity 2022.3.50f1
- Oculus Rift/Quest
- Windows 11

### Install

1. Install Neo4J
    - Go to their [download page](https://neo4j.com/deployment-center/#gdb-tab)
    - Scroll down to the section `Graph Database 
Self-Managed`
    - Select the `Community` tab to select the Community Edition
    - Select `Windows Executable`
    - Click `Download`
    
2. Set up language server
3. To run, do the following steps:

### Download the required whisper model here:

https://huggingface.co/ggerganov/whisper.cpp/tree/main

I'm using ggml-small.en-q8_0.bin model.
Put the downloaded model under ```Assets/Voice/Whisper```, and modify the corresponding "Model Path" in Whisper's Gameobject under ```WhisperManager```.
