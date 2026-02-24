from fastapi import FastAPI, UploadFile, File
import nemo.collections.asr as nemo_asr
import shutil
import os

app = FastAPI()

model = nemo_asr.models.ASRModel.from_pretrained(
    "nvidia/parakeet-rnnt-110m-da-dk"
)

@app.post("/transcribe")
async def transcribe(file: UploadFile = File(...)):
    file_path = f"temp_{file.filename}"

    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    # Transcribe
    result = model.transcribe([file_path])[0]

    os.remove(file_path)

    return {"transcript": result}