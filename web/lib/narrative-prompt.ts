import { z } from "zod";
import {
  NarrativeOptionSchema,
  type NarrativeEventRequest,
  type NarrativeEventResponse,
  type SimStateBucket,
} from "@/lib/narrative-templates";

const LlmPayloadSchema = z.object({
  headline: z.string().min(1).max(200),
  body: z.string().min(1).max(2000),
  options: z.array(NarrativeOptionSchema).min(1).max(4),
});

const LLM_TIMEOUT_MS = 15_000;

export function isNarrativeLlmConfigured(): boolean {
  return resolveNarrativeLlmProvider() !== null;
}

function resolveNarrativeLlmProvider(): "openai" | "anthropic" | null {
  const explicit = process.env.NARRATIVE_LLM_PROVIDER?.trim().toLowerCase();
  if (explicit === "openai" && process.env.OPENAI_API_KEY) return "openai";
  if (explicit === "anthropic" && process.env.ANTHROPIC_API_KEY) return "anthropic";
  if (process.env.OPENAI_API_KEY) return "openai";
  if (process.env.ANTHROPIC_API_KEY) return "anthropic";
  return null;
}

function buildSystemPrompt(): string {
  return [
    "You are the editor of The Daily Herald, a city newspaper in a city-builder simulation.",
    "Write grounded civic journalism from structured sim context only — no fantasy or unrelated topics.",
    "Respond with a single JSON object: { headline, body, options }.",
    "options is an array of 2-3 objects: { id: snake_case, label: short action, tradeoff: concrete sim consequence }.",
    "Keep headline under 120 characters. Body 2-4 sentences. No markdown fences.",
  ].join(" ");
}

function buildUserPrompt(
  bucket: SimStateBucket,
  context: NarrativeEventRequest["context"] | undefined,
  template: NarrativeEventResponse,
): string {
  const lines = [
    `Story bucket: ${bucket}`,
    context?.cityName ? `City name: ${context.cityName}` : null,
    context?.era ? `Era: ${context.era}` : null,
    context?.metricValue !== undefined ? `Key metric: ${context.metricValue}` : null,
    "",
    "Template reference (tone and stakes — rewrite, do not copy verbatim):",
    `Headline: ${template.headline}`,
    `Body: ${template.body}`,
    "Options:",
    ...template.options.map((o) => `- ${o.id}: ${o.label} (${o.tradeoff})`),
  ];
  return lines.filter((line) => line !== null).join("\n");
}

function extractJsonObject(text: string): unknown {
  const trimmed = text.trim();
  const fenceMatch = trimmed.match(/```(?:json)?\s*([\s\S]*?)```/i);
  const candidate = fenceMatch ? fenceMatch[1].trim() : trimmed;
  const start = candidate.indexOf("{");
  const end = candidate.lastIndexOf("}");
  if (start === -1 || end === -1 || end <= start) return null;
  try {
    return JSON.parse(candidate.slice(start, end + 1));
  } catch {
    return null;
  }
}

async function callOpenAi(system: string, user: string): Promise<string | null> {
  const apiKey = process.env.OPENAI_API_KEY;
  if (!apiKey) return null;

  const model = process.env.NARRATIVE_OPENAI_MODEL?.trim() || "gpt-4o-mini";
  const res = await fetch("https://api.openai.com/v1/chat/completions", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      temperature: 0.7,
      response_format: { type: "json_object" },
      messages: [
        { role: "system", content: system },
        { role: "user", content: user },
      ],
    }),
    signal: AbortSignal.timeout(LLM_TIMEOUT_MS),
  });

  if (!res.ok) {
    console.error("[narrative-prompt] OpenAI error:", res.status, await res.text());
    return null;
  }

  const json = (await res.json()) as {
    choices?: Array<{ message?: { content?: string } }>;
  };
  return json.choices?.[0]?.message?.content ?? null;
}

async function callAnthropic(system: string, user: string): Promise<string | null> {
  const apiKey = process.env.ANTHROPIC_API_KEY;
  if (!apiKey) return null;

  const model =
    process.env.NARRATIVE_ANTHROPIC_MODEL?.trim() || "claude-3-5-haiku-latest";
  const res = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "x-api-key": apiKey,
      "anthropic-version": "2023-06-01",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model,
      max_tokens: 1024,
      system,
      messages: [{ role: "user", content: user }],
    }),
    signal: AbortSignal.timeout(LLM_TIMEOUT_MS),
  });

  if (!res.ok) {
    console.error("[narrative-prompt] Anthropic error:", res.status, await res.text());
    return null;
  }

  const json = (await res.json()) as {
    content?: Array<{ type?: string; text?: string }>;
  };
  const block = json.content?.find((c) => c.type === "text");
  return block?.text ?? null;
}

async function callNarrativeLlm(system: string, user: string): Promise<string | null> {
  const provider = resolveNarrativeLlmProvider();
  if (!provider) return null;
  if (provider === "openai") return callOpenAi(system, user);
  return callAnthropic(system, user);
}

export async function generateNarrativeWithLlm(params: {
  bucket: SimStateBucket;
  context?: NarrativeEventRequest["context"];
  template: NarrativeEventResponse;
}): Promise<NarrativeEventResponse | null> {
  const system = buildSystemPrompt();
  const user = buildUserPrompt(params.bucket, params.context, params.template);
  const raw = await callNarrativeLlm(system, user);
  if (!raw) return null;

  const parsed = LlmPayloadSchema.safeParse(extractJsonObject(raw));
  if (!parsed.success) {
    console.error("[narrative-prompt] invalid LLM JSON:", parsed.error.flatten());
    return null;
  }

  return {
    bucket: params.bucket,
    headline: parsed.data.headline,
    body: parsed.data.body,
    options: parsed.data.options,
    source: "llm",
  };
}
