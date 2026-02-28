from fastapi import FastAPI, File, UploadFile
from fastapi.middleware.cors import CORSMiddleware
import torch
import nemo.collections.asr as nemo_asr
import io
from pydub import AudioSegment

# Initialize FastAPI app
app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:8080", "http://127.0.0.1:8080", "http://0.0.0.0:8080", "http://host.docker.internal:8080", "http://verbalcards:8080"],  # Your Razor Page URL
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Load the Parakeet model
asr_model = nemo_asr.models.ASRModel.from_pretrained(
    model_name="nvidia/parakeet-rnnt-110m-da-dk"
)

@app.post("/transcribe")
async def transcribe(file: UploadFile = File(...)):
    # Read and convert audio
    audio_data = await file.read()
    audio = AudioSegment.from_file(io.BytesIO(audio_data))
    audio = audio.set_channels(1).set_frame_rate(16000)
    
    # Save to temporary file
    temp_path = "temp.wav"
    audio.export(temp_path, format="wav")
    
    # Transcribe
    transcript = asr_model.transcribe([temp_path])
    return {"transcription": transcript[0].text} 