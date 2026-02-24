import nemo.collections.asr as nemo_asr
from fastapi import FastAPI, UploadFile

app = FastAPI()

@app.post("/transcribe")
async def transcribe(file: UploadFile):
    model = nemo_asr.models.ASRModel.from_pretrained(
        "nvidia/parakeet-rnnt-110m-da-dk"
    )
    
    file_path = f"temp_{file.filename}"

    with open(file_path, "wb") as buffer:
        buffer.write(await file.read())

    result = model.transcribe([file_path])[0]

    return {"transcript": result}
