import eventsJson from "../../base/data/events/events.json";
import {
  narrativeFromBucket,
  type NarrativeEventResponse,
  type SimStateBucket,
} from "@/lib/narrative-templates";

type EventRecord = {
  id: string | number;
  typeId?: string;
  name: string;
  category: string;
  description: string;
};

const EVENTS = eventsJson as EventRecord[];

function eventLookupKey(record: EventRecord): string {
  if (typeof record.id === "string") return record.id.toLowerCase();
  if (record.typeId) return record.typeId.toLowerCase();
  return String(record.id).toLowerCase();
}

const eventsById = new Map(EVENTS.map((e) => [eventLookupKey(e), e]));

/** Map sim event typeId + events.json metadata to a narrative template bucket. */
export function deriveBucketFromEventType(typeId: string): SimStateBucket {
  const def = eventsById.get(typeId.toLowerCase());
  const haystack = `${typeId} ${def?.name ?? ""} ${def?.description ?? ""} ${def?.category ?? ""}`.toLowerCase();

  if (
    /crime|riot|theft|arson|gang|vandal|burglary|assault|police/.test(haystack)
  ) {
    return "crime_rising";
  }
  if (
    /pollution|smog|emission|toxic|contamination|acid_rain|smoke/.test(haystack)
  ) {
    return "pollution_spike";
  }
  if (/traffic|congestion|gridlock|commute|transit_delay/.test(haystack)) {
    return "traffic_congestion";
  }
  if (
    /housing|homeless|rent|evict|afford|tenement|squatter/.test(haystack)
  ) {
    return "housing_shortage";
  }
  if (
    /disease|epidemic|plague|cholera|hospital|clinic|health|pandemic/.test(
      haystack,
    )
  ) {
    return "healthcare_low";
  }
  if (
    /recession|bankrupt|deficit|shortfall|debt|austerity|treasury|unemployment|strike/.test(
      haystack,
    )
  ) {
    return "budget_crisis";
  }
  if (
    /boom|prosper|invest|expansion|growth|surplus|fortune|gold_rush/.test(
      haystack,
    )
  ) {
    return "prosperity_high";
  }
  if (
    /protest|unrest|dissent|approval|morale|unhappy|riot|discontent/.test(
      haystack,
    )
  ) {
    return "happiness_low";
  }

  switch (def?.category) {
    case "economic":
      return "budget_crisis";
    case "social":
      return "happiness_low";
    case "political":
      return "happiness_low";
    case "infrastructure":
      return "traffic_congestion";
    default:
      return "default";
  }
}

export function getEventDefinition(typeId: string): EventRecord | undefined {
  return eventsById.get(typeId.toLowerCase());
}

export type EventMarkerStyle = {
  glyph: string;
  color: string;
};

const TYPE_MARKER_GLYPHS: Record<string, string> = {
  fire: "🔥",
  flood: "🌊",
  wildfire: "🔥",
  drought: "☀️",
  earthquake: "🫨",
  tornado: "🌪️",
  blizzard: "❄️",
  heatwave: "🥵",
  protest: "📢",
  strike: "✊",
  riot: "⚠️",
  crime_wave: "🚨",
  epidemic: "🦠",
  pandemic: "🦠",
};

const CATEGORY_MARKER_COLORS: Record<string, string> = {
  natural: "#ff6b35",
  economic: "#f4a261",
  social: "#e63946",
  political: "#9b5de5",
  infrastructure: "#4cc9f0",
};

/** Visual style for in-world event markers (glyph + accent color). */
export function getEventMarkerStyle(typeId: string): EventMarkerStyle {
  const id = typeId.toLowerCase();
  const def = getEventDefinition(id);
  const glyph = TYPE_MARKER_GLYPHS[id] ?? "⚠️";
  const color =
    CATEGORY_MARKER_COLORS[def?.category ?? ""] ?? "#ffd166";
  return { glyph, color };
}

export function getEventMarkerLabel(typeId: string): string {
  return getEventDefinition(typeId)?.name ?? typeId;
}

/** Build a Herald story from a live sim event using events.json copy + template options. */
export function narrativeFromSimEvent(typeId: string): NarrativeEventResponse {
  const def = getEventDefinition(typeId);
  const bucket = deriveBucketFromEventType(typeId);
  const template = narrativeFromBucket(bucket);

  return {
    ...template,
    headline: def?.name ?? template.headline,
    body: def?.description ?? template.body,
  };
}
