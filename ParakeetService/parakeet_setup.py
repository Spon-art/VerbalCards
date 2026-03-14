import torch
import nemo.collections.asr as nemo_asr

# Load model
asr_model = nemo_asr.models.ASRModel.from_pretrained(
    model_name="nvidia/parakeet-rnnt-110m-da-dk"
)

transcript = asr_model.transcribe(["mono.wav"])
print(transcript[0])
