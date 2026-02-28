from fastapi import FastAPI, File, UploadFile, Form
from pydub import AudioSegment
import io
import torch
import nemo.collections.asr as nemo_asr

app = FastAPI()

# Load the Parakeet model
asr_model = nemo_asr.models.ASRModel.from_pretrained(model_name="nvidia/parakeet-rnnt-110m-da-dk")

@app.post("/transcribe")
async def transcribe(file: UploadFile = File(...), include_timestamps: bool = Form(False)):
    # Read audio file
    audio_data = await file.read()
    audio_segment = AudioSegment.from_file(io.BytesIO(audio_data), format=file.content_type.split("/")[-1])
    audio_segment = audio_segment.set_channels(1).set_frame_rate(16000)  # Required by Parakeet
    audio_bytes = io.BytesIO()
    audio_segment.export(audio_bytes, format="wav")
    audio_bytes.seek(0)

    # Transcribe
    transcript = asr_model.transcribe([audio_bytes.getvalue()])
    return {"transcription": transcript[0]}   