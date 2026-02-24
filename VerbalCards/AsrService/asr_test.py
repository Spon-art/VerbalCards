import nemo.collections.asr as nemo_asr

model = nemo_asr.models.ASRModel.from_pretrained(
    "nvidia/parakeet-rnnt-110m-da-dk"
)

transcription = model.transcribe(["/Users/Jake/Desktop/ITU/VerbalCards/VerbalCards/AsrService/DAN_F_GreteT.wav"])
print(transcription)