FROM python:3.11-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY app ./app
RUN useradd --create-home --uid 10004 notifications
USER notifications
EXPOSE 5103
HEALTHCHECK --interval=10s --timeout=3s --start-period=25s --retries=5 CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:5103/health', timeout=2)"
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "5103"]
