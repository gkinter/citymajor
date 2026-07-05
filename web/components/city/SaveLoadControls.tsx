"use client";

import { useCallback, useEffect, useState, type CSSProperties } from "react";
import type { Entitlements } from "@/lib/entitlements";
import {
  HUD_COLORS,
  HUD_RADIUS,
  HUD_Z,
  HUD_ZONE,
  hudActionButton,
  hudModalShell,
  hudPanel,
} from "@/lib/hud-theme";
import type { SimClientApi, SimSnapshot } from "@/lib/sim-bridge";
import {
  CreateSaveResponseSchema,
  GetSaveResponseSchema,
  SaveListResponseSchema,
  type SaveSlotListItem,
} from "@/lib/saves-client";
import { FORMAT_VERSION_CMJR_V1 } from "@/lib/save-format";

type SaveLoadControlsProps = {
  simApi: SimClientApi | null;
  entitlements: Entitlements | null;
  onSlotsChanged?: () => void;
  onSaveSuccess?: () => void;
  onLoadSuccess?: () => void;
};

const barStyle: CSSProperties = {
  ...HUD_ZONE.leftStack,
  display: "flex",
  alignItems: "center",
  gap: 8,
  pointerEvents: "auto",
};

const slotBadgeStyle: CSSProperties = hudPanel({
  padding: "8px 10px",
  fontSize: 11,
  color: HUD_COLORS.textMuted,
  background: HUD_COLORS.panelBgSoft,
  border: `1px solid ${HUD_COLORS.borderSubtle}`,
  pointerEvents: "none",
});

const overlayStyle: CSSProperties = {
  position: "fixed",
  inset: 0,
  zIndex: HUD_Z.modal,
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  background: HUD_COLORS.overlay,
  padding: 16,
};

const modalHeaderStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: `1px solid ${HUD_COLORS.borderSubtle}`,
};

const listStyle: CSSProperties = {
  flex: 1,
  overflowY: "auto",
  padding: "8px 12px 16px",
};

const rowStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  gap: 12,
  padding: "10px 12px",
  marginTop: 8,
  borderRadius: HUD_RADIUS.sm,
  border: `1px solid ${HUD_COLORS.borderSubtle}`,
  background: HUD_COLORS.rowBg,
};

function formatWhen(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
}

function isZoneSnapshot(
  value: unknown,
): value is NonNullable<SimSnapshot["zones"]>[number] {
  if (typeof value !== "object" || value === null) return false;
  const zone = value as Record<string, unknown>;
  return (
    typeof zone.tileX === "number" &&
    typeof zone.tileZ === "number" &&
    typeof zone.zoneType === "number"
  );
}

function isRoadSnapshot(value: unknown): value is NonNullable<SimSnapshot["roads"]>[number] {
  if (typeof value !== "object" || value === null) return false;
  const road = value as Record<string, unknown>;
  return (
    typeof road.tileX === "number" &&
    typeof road.tileZ === "number" &&
    typeof road.roadFlags === "number"
  );
}

function parseSimSnapshot(payload: Record<string, unknown>): SimSnapshot | null {
  if (
    typeof payload.tick !== "number" ||
    typeof payload.population !== "number" ||
    typeof payload.cityFunds !== "number" ||
    typeof payload.era !== "number" ||
    !Array.isArray(payload.buildings)
  ) {
    return null;
  }

  const zones = Array.isArray(payload.zones)
    ? payload.zones.filter(isZoneSnapshot)
    : [];
  const roads = Array.isArray(payload.roads)
    ? payload.roads.filter(isRoadSnapshot)
    : [];

  return {
    ...(payload as SimSnapshot),
    zones,
    roads,
  };
}

function defaultSaveName(): string {
  const d = new Date();
  return `City ${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
}

export function SaveLoadControls({
  simApi,
  entitlements,
  onSlotsChanged,
  onSaveSuccess,
  onLoadSuccess,
}: SaveLoadControlsProps) {
  const [loadOpen, setLoadOpen] = useState(false);
  const [saves, setSaves] = useState<SaveSlotListItem[]>([]);
  const [slotCount, setSlotCount] = useState(0);
  const [maxSlots, setMaxSlots] = useState(entitlements?.maxSaveSlots ?? 3);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [loadingSlotId, setLoadingSlotId] = useState<string | null>(null);

  const refreshList = useCallback(async () => {
    setListLoading(true);
    setListError(null);
    try {
      const res = await fetch("/api/saves", { credentials: "include" });
      const json: unknown = await res.json();
      if (!res.ok) {
        const message =
          typeof json === "object" &&
          json !== null &&
          "error" in json &&
          typeof (json as { error: unknown }).error === "string"
            ? (json as { error: string }).error
            : `HTTP ${res.status}`;
        throw new Error(message);
      }
      const parsed = SaveListResponseSchema.safeParse(json);
      if (!parsed.success) throw new Error("Invalid saves list response");
      setSaves(parsed.data.saves);
      setSlotCount(parsed.data.count);
      setMaxSlots(parsed.data.maxSlots);
    } catch (err) {
      setListError(err instanceof Error ? err.message : "Failed to load saves");
    } finally {
      setListLoading(false);
    }
  }, []);

  useEffect(() => {
    if (entitlements) setMaxSlots(entitlements.maxSaveSlots);
  }, [entitlements]);

  useEffect(() => {
    void refreshList();
  }, [refreshList]);

  useEffect(() => {
    if (loadOpen) void refreshList();
  }, [loadOpen, refreshList]);

  const handleSave = useCallback(async () => {
    if (!simApi) {
      setActionMessage("Sim not ready yet");
      return;
    }
    const snapshot = simApi.getSnapshot();
    if (!snapshot) {
      setActionMessage("No sim snapshot to save");
      return;
    }

    const name = window.prompt("Save name", defaultSaveName());
    if (!name?.trim()) return;

    setSaving(true);
    setActionMessage(null);
    try {
      let wasmBlobBase64: string | null = null;
      try {
        wasmBlobBase64 = await simApi.exportWasmSave();
      } catch (err) {
        console.warn("[SaveLoadControls] WASM export failed, saving JSON snapshot only:", err);
      }
      const res = await fetch("/api/saves", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: name.trim(),
          payload: snapshot,
          ...(wasmBlobBase64 ? { wasmBlobBase64 } : {}),
        }),
      });
      const json: unknown = await res.json();
      if (!res.ok) {
        const message =
          typeof json === "object" &&
          json !== null &&
          "error" in json &&
          typeof (json as { error: unknown }).error === "string"
            ? (json as { error: string }).error
            : `HTTP ${res.status}`;
        throw new Error(message);
      }
      const parsed = CreateSaveResponseSchema.safeParse(json);
      if (!parsed.success) throw new Error("Invalid save response");
      setSlotCount(parsed.data.count);
      setMaxSlots(parsed.data.maxSlots);
      setActionMessage(`Saved "${parsed.data.save.name}"`);
      onSlotsChanged?.();
      onSaveSuccess?.();
      // Refresh so a subsequent Load dialog open shows the new slot without
      // needing a full page reload — refreshList also covers concurrent edits
      // from another tab.
      void refreshList();
    } catch (err) {
      setActionMessage(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }, [simApi, onSlotsChanged, onSaveSuccess, refreshList]);

  const handleLoad = useCallback(
    async (slot: SaveSlotListItem) => {
      if (!simApi) {
        setActionMessage("Sim not ready yet");
        return;
      }
      setLoadingSlotId(slot.id);
      setActionMessage(null);
      try {
        const res = await fetch(`/api/saves/${slot.id}`, { credentials: "include" });
        const json: unknown = await res.json();
        if (!res.ok) {
          const message =
            typeof json === "object" &&
            json !== null &&
            "error" in json &&
            typeof (json as { error: unknown }).error === "string"
              ? (json as { error: string }).error
              : `HTTP ${res.status}`;
          throw new Error(message);
        }
        const parsed = GetSaveResponseSchema.safeParse(json);
        if (!parsed.success) throw new Error("Invalid save response");

        if (
          parsed.data.save.formatVersion >= FORMAT_VERSION_CMJR_V1 &&
          parsed.data.save.wasmBlobBase64
        ) {
          await simApi.applyWasmSave(parsed.data.save.wasmBlobBase64);
        } else {
          const snapshot = parseSimSnapshot(parsed.data.save.payload ?? {});
          if (!snapshot) {
            setActionMessage(`Save "${slot.name}" has no restorable snapshot`);
            return;
          }
          await simApi.applySnapshot(snapshot);
        }

        setLoadOpen(false);
        setActionMessage(`Loaded "${slot.name}"`);
        onLoadSuccess?.();
      } catch (err) {
        setActionMessage(err instanceof Error ? err.message : "Load failed");
      } finally {
        setLoadingSlotId(null);
      }
    },
    [simApi, onLoadSuccess],
  );

  const slotsFull = slotCount >= maxSlots;

  return (
    <>
      <div style={barStyle} role="group" aria-label="Save and load">
        <button
          type="button"
          style={hudActionButton(saving || slotsFull)}
          disabled={saving || slotsFull}
          title={slotsFull ? `All ${maxSlots} save slots in use` : "Save current city"}
          onClick={() => void handleSave()}
        >
          {saving ? "Saving…" : "Save"}
        </button>
        <button
          type="button"
          style={hudActionButton(!!loadingSlotId)}
          disabled={!!loadingSlotId}
          onClick={() => setLoadOpen(true)}
        >
          {loadingSlotId ? "Loading…" : "Load"}
        </button>
        <span style={slotBadgeStyle} aria-live="polite">
          slots {slotCount}/{maxSlots}
        </span>
      </div>

      {actionMessage ? (
        <div
          style={{
            ...HUD_ZONE.toast,
            ...hudPanel({
              padding: "6px 10px",
              fontSize: 11,
              color: HUD_COLORS.toast,
              pointerEvents: "none",
            }),
          }}
        >
          {actionMessage}
        </div>
      ) : null}

      {loadOpen ? (
        <div
          style={overlayStyle}
          role="dialog"
          aria-modal="true"
          aria-labelledby="load-saves-title"
          onClick={() => setLoadOpen(false)}
        >
          <div style={hudModalShell()} onClick={(e) => e.stopPropagation()}>
            <div style={modalHeaderStyle}>
              <div>
                <div id="load-saves-title" style={{ fontWeight: 700 }}>
                  Load city
                </div>
                <div style={{ fontSize: 11, opacity: 0.7, marginTop: 4 }}>
                  {slotCount}/{maxSlots} slots used
                </div>
              </div>
              <button
                type="button"
                style={{ ...hudActionButton(), padding: "4px 10px", fontSize: 11 }}
                onClick={() => setLoadOpen(false)}
              >
                Close
              </button>
            </div>

            <div style={listStyle}>
              {listLoading ? (
                <div style={{ padding: 12, opacity: 0.7 }}>Loading saves…</div>
              ) : listError ? (
                <div style={{ padding: 12, color: HUD_COLORS.error }}>{listError}</div>
              ) : saves.length === 0 ? (
                <div style={{ padding: 12, opacity: 0.7 }}>No saves yet.</div>
              ) : (
                saves.map((slot) => (
                  <div key={slot.id} style={rowStyle}>
                    <div>
                      <div style={{ fontWeight: 600 }}>{slot.name}</div>
                      <div style={{ fontSize: 11, opacity: 0.65, marginTop: 4 }}>
                        Pop {slot.summary.population.toLocaleString()} · $
                        {slot.summary.cityFunds.toLocaleString()} · Era {slot.summary.era}
                      </div>
                      <div style={{ fontSize: 11, opacity: 0.5, marginTop: 2 }}>
                        Updated {formatWhen(slot.updatedAt)}
                      </div>
                    </div>
                    <button
                      type="button"
                      style={{
                        ...hudActionButton(loadingSlotId === slot.id),
                        padding: "6px 12px",
                        fontSize: 11,
                      }}
                      disabled={loadingSlotId === slot.id}
                      onClick={() => void handleLoad(slot)}
                    >
                      {loadingSlotId === slot.id ? "Loading…" : "Load"}
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      ) : null}
    </>
  );
}
