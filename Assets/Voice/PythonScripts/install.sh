# download whisper model and place in correct place
(
cd ../Whisper &&
wget https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en-q5_1.bin
)

# create and activate virtual environment
python3 -m venv .venv
source .venv/bin/activate

# install python packages
pip install -r requirements.txt

# deactivate virtual environment
deactivate