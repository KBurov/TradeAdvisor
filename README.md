# TradeAdvisor

**TradeAdvisor** is a hybrid **Python + .NET** research and forecasting platform for U.S. stocks and ETFs.  
It combines classical financial analysis (technical indicators, fundamentals, ARIMA/GARCH, chart patterns) with modern AI approaches (LSTM, Transformers, ensembles) and sentiment analysis from news and social media.  

The system is designed as a **microservices architecture** with REST-only APIs, PostgreSQL as the primary datastore, and Docker/Kubernetes for deployment.  
Models are trained in Python, exported to ONNX, and served via high-performance .NET inference services.  

---

## Getting Started
- [Setup Guide (Windows + WSL2)](docs/setup-windows-wsl.md)  
- [Architecture Overview](docs/architecture.md)  
- [Architecture Decision Records (ADRs)](docs/adr/)  
- [Roadmap](docs/roadmap.md)  
- [Database Schema](docs/schema.md)  
- [Services Overview](docs/services.md)  

---

## Goals
- Flexible stock/ETF forecasting  
- Combine classical + AI models (ARIMA, LSTM, Transformers, ensembles)  
- High-performance .NET services for ingest, features, inference, and backtesting  
- REST-only APIs, cloud-native architecture  
- Reproducible research with model registry (MLflow) and artifact storage (MinIO/S3)  
