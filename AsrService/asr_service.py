from transformers import pipeline
import librosa

# Load audio file (librosa automatically resamples to 22.05kHz by default)
audio, sr = librosa.load("/app/DAN_F_GreteT.wav", sr=16000)  # Force 16kHz

# Transcribe
transcriber = pipeline(model="alexandrainst/roest-315m")
result = transcriber(audio)

print(result['text'])