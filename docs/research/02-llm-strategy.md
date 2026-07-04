# Research: 02 Llm Strategy

The full research report is complete. Here is a summary of the key findings:

---

## Top-Line Answer: Yes, highly viable.

Fine-tuning and self-hosting small LLMs for Iron & Oak is not only feasible -- it is the economically superior approach at any meaningful player scale.

### The Numbers That Matter

**Fine-tuning cost**: Under $5 in compute. The real cost is 40-80 hours of human time curating 3,000-4,000 training examples in your game's voice and format.

**Quality**: A fine-tuned Qwen 2.5 14B outperforms GPT-4o-mini by ~22% on constrained creative writing tasks (CogWriter study). For in-domain content like your game's newspapers and debates, the fine-tuned model will be strictly better because it has learned your world's tone, vocabulary, and output format.

**Self-hosting vs API**: At 10,000 CCU, self-hosted is **7-9x cheaper** than GPT-4o-mini API ($10,800/mo vs $77,760/mo). The break-even point where self-hosting clearly wins is around 1,000 DAU.

### Recommended Architecture

Three models, one family (Qwen 2.5, all Apache 2.0 licensed):

| Model | Role | Where |
|-------|------|-------|
| **14B** (INT8) | Council debates, trade negotiations | Your server (A100/H100) |
| **7B** (FP16) | Newspapers, quests, crisis narration | Your server (RTX 4090) |
| **3B** (Q4 GGUF) | Citizen stories, flavor text | Player's PC (optional Steam DLC, ~2 GB) |

Serving stack: **vLLM** (server), **llama.cpp** (client), **Redis semantic cache** (60-80% hit rate), pre-generated content pool as fallback.

### Critical Lessons from Industry

1. Never let the LLM own game state -- it reads state and generates text, the engine maintains truth
2. Don't give players a blank text box -- structured choices with AI narration
3. Pre-generate during loading screens to hide latency
4. Use hierarchical summarization + RAG for long-term memory (AI Dungeon's proven approach)

The full report with detailed hardware specs, cost tables, throughput benchmarks, VRAM requirements, training data format examples, and a phased scaling plan is in the plan file.