import uuid
import asyncio
from fastapi import FastAPI, UploadFile

import nemo.collections.asr as nemo_asr

app = FastAPI()

# Load model once (VERY IMPORTANT for performance)
model = nemo_asr.models.ASRModel.from_pretrained(
    "nvidia/parakeet-rnnt-110m-da-dk"
)

# In-memory job storage (replace with MongoDB later)
jobs = {}


@app.post("/transcribe")
async def transcribe(file: UploadFile):
    job_id = str(uuid.uuid4())

    # Mark job as processing
    jobs[job_id] = {
        "status": "processing",
        "result": None
    }

    # Read audio
    audio_bytes = await file.read()

    # Save temporary file (NeMo usually wants file path)
    temp_path = f"/tmp/{job_id}.wav"

    with open(temp_path, "wb") as f:
        f.write(audio_bytes)

    # Run ASR in background
    asyncio.create_task(run_asr(job_id, temp_path))

    return {
        "job_id": job_id,
        "status": "processing"
    }


async def run_asr(job_id, path):
    try:
        transcription = model.transcribe([path])[0]

        jobs[job_id]["status"] = "done"
        jobs[job_id]["result"] = transcription

    except Exception as e:
        jobs[job_id]["status"] = "failed"
        jobs[job_id]["result"] = str(e)


@app.get("/transcribe/status/{job_id}")
async def get_status(job_id: str):
    if job_id not in jobs:
        return {"status": "not_found"}

    return jobs[job_id]


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)