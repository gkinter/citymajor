"use client";

import { useCallback, useEffect, useState, type CSSProperties } from "react";
import type { Entitlements } from "@/lib/entitlements";
import type { SimClientApi, SimSnapshot } from "@/lib/sim-bridge";
import {
  CreateSaveResponseSchema,
  SaveListResponseSchema,
  type SaveSlot,
} from "@/lib/saves-client";

type SaveLoadControlsProps = {
  simApi: SimClientApi | null;
  entitlements: Entitlements | null;
  onSlotsChanged?: () => void;
};

const barStyle: CSSProperties = {
  position: "absolute",
  top: 56,
  left: 12,
  zIndex: 15,
  display: "flex",
  alignItems: "center",
  gap: 8,
  pointerEvents: "auto",
};

const buttonStyle: CSSProperties = {
  padding: "8px 14px",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 12,
  fontWeight: 600,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.82)",
  border: "1px solid rgba(120, 160, 220, 0.25)",
  borderRadius: 8,
  cursor: "pointer",
};

const slotBadgeStyle: CSSProperties = {
  padding: "8px 10px",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 11,
  color: "rgba(232, 238, 248, 0.75)",
  background: "rgba(8, 12, 24, 0.65)",
  border: "1px solid rgba(120, 160, 220, 0.18)",
  borderRadius: 8,
  pointerEvents: "none",
};

const overlayStyle: CSSProperties = {
  position: "fixed",
  inset: 0,
  zIndex: 40,
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  background: "rgba(4, 8, 16, 0.72)",
  padding: 16,
};

const modalStyle: CSSProperties = {
  width: "min(480px, 100%)",
  maxHeight: "min(70vh, 560px)",
  display: "flex",
  flexDirection: "column",
  fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
  fontSize: 13,
  color: "#e8eef8",
  background: "rgba(8, 12, 24, 0.96)",
  border: "1px solid rgba(120, 160, 220, 0.3)",
  borderRadius: 10,
  boxShadow: "0 16px 48px rgba(0, 0, 0, 0.5)",
};

const modalHeaderStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "14px 16px",
  borderBottom: "1px solid rgba(120, 160, 220, 0.2)",
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
  borderRadius: 6,
  border: "1px solid rgba(120, 160, 220, 0.18)",
  background: "rgba(16, 22, 38, 0.45)",
};

function formatWhen(iso: string): string {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
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
  return payload as SimSnapshot;
}

function defaultSaveName(): string {
  const d = new Date();
  return `City ${d.toLocaleDateString()} ${d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
}

export function SaveLoadControls({
  simApi,
  entitlements,
  onSlotsChanged,
}: SaveLoadControlsProps) {
  const [loadOpen, setLoadOpen] = useState(false);
  const [saves, setSaves] = useState<SaveSlot[]>([]);
  const [slotCount, setSlotCount] = useState(0);
  const [maxSlots, setMaxSlots] = useState(entitlements?.maxSaveSlots ?? 3);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

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
      const res = await fetch("/api/saves", {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: name.trim(), payload: snapshot }),
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
    } catch (err) {
      setActionMessage(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }, [simApi, onSlotsChanged]);

  const handleLoad = useCallback(
    (slot: SaveSlot) => {
      if (!simApi) {
        setActionMessage("Sim not ready yet");
        return;
      }
      const snapshot = parseSimSnapshot(slot.payload);
      if (!snapshot) {
        setActionMessage(`Save "${slot.name}" has no restorable snapshot`);
        return;
      }
      simApi.applySnapshot(snapshot);
      setLoadOpen(false);
      setActionMessage(`Loaded "${slot.name}"`);
    },
    [simApi],
  );

  const slotsFull = slotCount >= maxSlots;

  return (
    <>
      <div style={barStyle} role="group" aria-label="Save and load">
        <button
          type="button"
          style={{
            ...buttonStyle,
            opacity: saving || slotsFull ? 0.55 : 1,
            cursor: saving || slotsFull ? "not-allowed" : "pointer",
          }}
          disabled={saving || slotsFull}
          title={slotsFull ? `All ${maxSlots} save slots in use` : "Save current city"}
          onClick={() => void handleSave()}
        >
          {saving ? "Saving…" : "Save"}
        </button>
        <button
          type="button"
          style={buttonStyle}
          onClick={() => setLoadOpen(true)}
        >
          Load
        </button>
        <span style={slotBadgeStyle} aria-live="polite">
          slots {slotCount}/{maxSlots}
        </span>
      </div>

      {actionMessage ? (
        <div
          style={{
            position: "absolute",
            top: 96,
            left: 12,
            zIndex: 15,
            padding: "6px 10px",
            fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
            fontSize: 11,
            color: "#c8e8ff",
            background: "rgba(8, 12, 24, 0.88)",
            border: "1px solid rgba(120, 160, 220, 0.25)",
            borderRadius: 6,
            pointerEvents: "none",
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
          <div style={modalStyle} onClick={(e) => e.stopPropagation()}>
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
                style={{ ...buttonStyle, padding: "4px 10px", fontSize: 11 }}
                onClick={() => setLoadOpen(false)}
              >
                Close
              </button>
            </div>

            <div style={listStyle}>
              {listLoading ? (
                <div style={{ padding: 12, opacity: 0.7 }}>Loading saves…</div>
              ) : listError ? (
                <div style={{ padding: 12, color: "#ffb4b4" }}>{listError}</div>
              ) : saves.length === 0 ? (
                <div style={{ padding: 12, opacity: 0.7 }}>No saves yet.</div>
              ) : (
                saves.map((slot) => (
                  <div key={slot.id} style={rowStyle}>
                    <div>
                      <div style={{ fontWeight: 600 }}>{slot.name}</div>
                      <div style={{ fontSize: 11, opacity: 0.65, marginTop: 4 }}>
                        Updated {formatWhen(slot.updatedAt)}
                      </div>
                    </div>
                    <button
                      type="button"
                      style={{ ...buttonStyle, padding: "6px 12px", fontSize: 11 }}
                      onClick={() => handleLoad(slot)}
                    >
                      Load
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
