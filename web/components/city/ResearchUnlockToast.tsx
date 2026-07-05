"use client";

import { useEffect, useMemo } from "react";
import type { CSSProperties } from "react";
import { HUD_COLORS, HUD_ZONE, hudPanel } from "@/lib/hud-theme";
import { formatUnlockLabel, techNameFromIndex } from "@/lib/tech-catalog";
import { ROAD_TIER_DEFINITIONS } from "@/lib/road-types";
import type { NewlyUnlockedResearch } from "@/lib/tech-unlocks";
import { ZONE_TIERS } from "@/lib/zone-tiers";

const zoneLabelByKey = new Map(ZONE_TIERS.map((tier) => [tier.contentKey, tier.label]));
const roadLabelByKey = new Map(
  ROAD_TIER_DEFINITIONS.map((road) => [road.contentKey, road.label]),
);

function classifyContentKey(
  contentKey: string,
): { kind: "zone" | "building" | "other"; label: string } {
  const zoneLabel = zoneLabelByKey.get(contentKey);
  if (zoneLabel) return { kind: "zone", label: zoneLabel };

  const roadLabel = roadLabelByKey.get(contentKey);
  if (roadLabel) return { kind: "other", label: `${roadLabel} roads` };

  const buildingLabel = formatUnlockLabel(contentKey);
  if (buildingLabel !== contentKey) return { kind: "building", label: buildingLabel };

  return {
    kind: "other",
    label: contentKey.replace(/_/g, " "),
  };
}

type ResearchUnlockToastProps = {
  unlock: NewlyUnlockedResearch | null;
  durationMs?: number;
  onDismiss?: () => void;
  style?: CSSProperties;
};

export function ResearchUnlockToast({
  unlock,
  durationMs = 5_000,
  onDismiss,
  style,
}: ResearchUnlockToastProps) {
  useEffect(() => {
    if (!unlock || !onDismiss) return;
    const id = window.setTimeout(onDismiss, durationMs);
    return () => window.clearTimeout(id);
  }, [unlock, durationMs, onDismiss]);

  const grouped = useMemo(() => {
    if (!unlock) return null;

    const zones: string[] = [];
    const buildings: string[] = [];
    const other: string[] = [];

    for (const contentKey of unlock.contentKeys) {
      const { kind, label } = classifyContentKey(contentKey);
      if (kind === "zone") zones.push(label);
      else if (kind === "building") buildings.push(label);
      else other.push(label);
    }

    const techNames = unlock.techIds
      .map((id) => techNameFromIndex(id))
      .filter((name): name is string => Boolean(name));

    return { techNames, zones, buildings, other };
  }, [unlock]);

  if (!unlock || !grouped) return null;

  const title =
    grouped.techNames.length === 1
      ? grouped.techNames[0]
      : grouped.techNames.length > 1
        ? `${grouped.techNames.slice(0, 2).join(", ")}${
            grouped.techNames.length > 2 ? ` +${grouped.techNames.length - 2}` : ""
          }`
        : "Research";

  const hasUnlocks =
    grouped.zones.length > 0 ||
    grouped.buildings.length > 0 ||
    grouped.other.length > 0;

  return (
    <div
      role="status"
      aria-live="polite"
      style={{
        ...HUD_ZONE.toast,
        ...hudPanel({
          padding: "10px 14px",
          fontSize: 12,
          color: HUD_COLORS.toast,
          pointerEvents: "none",
          maxWidth: 340,
          lineHeight: 1.45,
        }),
        ...style,
      }}
    >
      <div style={{ fontWeight: 600, marginBottom: hasUnlocks ? 6 : 0 }}>
        Research complete: {title}
      </div>
      {hasUnlocks ? (
        <ul style={{ margin: 0, padding: "0 0 0 16px", opacity: 0.9 }}>
          {grouped.zones.length > 0 ? (
            <li>Zones: {grouped.zones.join(", ")}</li>
          ) : null}
          {grouped.buildings.length > 0 ? (
            <li>Buildings: {grouped.buildings.join(", ")}</li>
          ) : null}
          {grouped.other.length > 0 ? (
            <li>Unlocks: {grouped.other.join(", ")}</li>
          ) : null}
        </ul>
      ) : null}
    </div>
  );
}
