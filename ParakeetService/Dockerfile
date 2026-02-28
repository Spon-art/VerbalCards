FROM python:3.10-slim

# Install system dependencies
RUN apt-get update && apt-get install -y \
    libsndfile1 \
    ffmpeg \
    gcc \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Install core dependencies first
RUN pip install --no-cache-dir Cython packaging

# Install Nemo after core packages
RUN pip install --no-cache-dir 'nemo_toolkit['asr']'

# Copy requirements.txt (if you have additional packages)
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]   