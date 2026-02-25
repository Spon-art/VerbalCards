# To run the docker setup inside the AsrService directory:
```zsh
docker build -t roest-asr .
docker run --rm -v $(pwd):/app roest-asr python asr_service.py
```

Make sure to have a valid .wav file to test in AsrService directory, and change it in asr_service.py.