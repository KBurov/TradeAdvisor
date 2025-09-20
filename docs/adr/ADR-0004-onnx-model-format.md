# ADR-0004: ONNX Model Format for Inference

## Context
Models will be trained primarily in Python (PyTorch, TensorFlow, LightGBM, etc.).  
Inference in .NET requires a portable format with strong runtime support.  
We want:
- Cross-platform portability between Python and .NET.
- Optimized runtime performance with GPU/CPU acceleration.
- Long-term ecosystem support.

## Decision
Export models to **ONNX (Open Neural Network Exchange)** format for inference.  
- Training: Python frameworks → export ONNX.
- Inference: .NET services → ONNX Runtime.
- Standardize input/output schema for consistency.

## Status
Accepted.

## Consequences
- Enables fast inference in .NET using ONNX Runtime with SIMD/GPU acceleration.
- Decouples training environment from serving environment.
- Requires discipline in model export & schema validation pipelines.
- Some complex models may need conversion tweaks or custom ops.
