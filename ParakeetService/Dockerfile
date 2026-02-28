FROM nvcr.io/nvidia/pytorch:24.01-py3

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y libsndfile1 ffmpeg

# Copy requirements file
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir torch torchvision torchaudio
RUN pip install --no-cache-dir Cython
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Expose port
EXPOSE 8000

# Run FastAPI app
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]   