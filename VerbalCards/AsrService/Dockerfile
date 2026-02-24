FROM python:3.10-slim

WORKDIR /app

# Install system dependencies (IMPORTANT for ASR!)
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libsndfile1 \
    git \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Upgrade pip
RUN pip install --upgrade pip

# Copy requirements first (cache optimization)
COPY requirements.txt .

# Install Python dependencies
RUN pip install -r requirements.txt

# Copy source code
COPY . .

# Expose ASR service port
EXPOSE 8000

# Run ASR server
CMD ["uvicorn", "asr_server:app", "--host", "0.0.0.0", "--port", "8000"]